using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;

namespace MinecraftHelper.Services
{
    internal readonly record struct InventoryMarkerLayout(int Left, int Top, int Scale)
    {
        private const int SlotStartX = 8;
        private const int SlotStartY = 84;
        private const int SlotStep = 18;

        public Point GetSlotCenter(int zeroBasedSlot)
        {
            int column = zeroBasedSlot % 9;
            int row = zeroBasedSlot / 9;
            return new Point(
                Left + (SlotStartX + column * SlotStep + 8) * Scale,
                Top + (SlotStartY + row * SlotStep + 8) * Scale);
        }
    }

    internal sealed class InventoryMarkerDetection
    {
        public InventoryMarkerLayout Layout { get; init; }
        public bool HasGuiMarkers { get; init; }
        public bool SupportsFullInventoryScan { get; init; }
        public IReadOnlyList<int> MarkedSlots { get; init; } = Array.Empty<int>();
        public IReadOnlyList<DetectedInventoryItem> Items { get; init; } = Array.Empty<DetectedInventoryItem>();
        public IReadOnlyList<int> AllNonCobblestoneSlots { get; init; } = Array.Empty<int>();
        public IReadOnlyList<DetectedInventoryItem> AllNonCobblestoneItems { get; init; } = Array.Empty<DetectedInventoryItem>();
        public IReadOnlyList<int> UnknownMarkerSlots { get; init; } = Array.Empty<int>();
        public IReadOnlyList<int> FullCobblestoneSlots { get; init; } = Array.Empty<int>();
    }

    internal readonly record struct DetectedInventoryItem(int Slot, string ItemId, int Quantity);

    internal static class InventoryMarkerDetector
    {
        private const int GuiWidth = 176;
        private const int GuiHeight = 166;
        private const int TopLeftMarkerX = 1;
        private const int TopLeftMarkerY = 1;
        private const int BottomRightMarkerX = 171;
        private const int BottomRightMarkerY = 162;
        private const int ItemMarkerX = 13;
        private const int ItemMarkerY = 0;
        private const int SlotStartX = 8;
        private const int SlotStartY = 84;
        private const int HotbarSlotStartY = 142;
        private const int SlotStep = 18;
        private const int MaximumGuiScale = 8;
        private const int ColorTolerance = 18;
        private const int MinimumFallbackMarkers = 2;
        private const int BlazingGuiWidth = 193;
        private const int BlazingGuiHeight = 196;
        private const int MinimumBlazingDarkBorders = 30;

        private static readonly string[] StackCount64Glyphs =
        {
            "..##.....##...",
            ".#......#.#...",
            "#......#..#...",
            "####..#...#...",
            "#...#.#####...",
            "#...#.....#...",
            ".###......#..."
        };

        // Bright foreground pixels of Minecraft 1.8.8's five-pixel-wide number glyphs.
        // Stack labels are right-aligned in the lower-right corner of the 16x16 icon.
        private static readonly string[][] StackCountDigitGlyphs =
        {
            new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            new[] { ".###.", "#...#", "....#", "..##.", "....#", "#...#", ".###." },
            new[] { "...##", "..#.#", ".#..#", "#...#", "#####", "....#", "....#" },
            new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." },
            new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." }
        };

        private static readonly MarkerColor M = new MarkerColor(255, 0, 255);
        private static readonly MarkerColor C = new MarkerColor(0, 255, 255);
        private static readonly MarkerColor Y = new MarkerColor(255, 255, 0);

        private static readonly MarkerColor[,] MarkerLocator =
        {
            { M, C },
            { C, Y }
        };

        private static readonly MarkerColor[,] LegacyItemMarker =
        {
            { M, C, M },
            { C, Y, C },
            { Y, M, Y }
        };

        private static readonly MarkerColor[] MarkerCodeColors = { M, C, Y };

        private static readonly ItemMarkerDefinition[] ItemMarkers =
        {
            new ItemMarkerDefinition("diamond", BuildItemMarker(0)),
            new ItemMarkerDefinition("gold_ingot", BuildItemMarker(1)),
            new ItemMarkerDefinition("iron_ingot", BuildItemMarker(2)),
            new ItemMarkerDefinition("obsidian", BuildItemMarker(3)),
            new ItemMarkerDefinition("apple", BuildItemMarker(4)),
            new ItemMarkerDefinition("sand", BuildItemMarker(5)),
            new ItemMarkerDefinition("gunpowder", BuildItemMarker(6)),
            new ItemMarkerDefinition("emerald", BuildItemMarker(7)),
            new ItemMarkerDefinition("coal", BuildItemMarker(8)),
            new ItemMarkerDefinition("quartz", BuildItemMarker(9)),
            new ItemMarkerDefinition("book", BuildItemMarker(10)),
            new ItemMarkerDefinition("ender_pearl", BuildItemMarker(11)),
            new ItemMarkerDefinition("redstone", BuildItemMarker(12)),
            new ItemMarkerDefinition("diamond_pickaxe", BuildItemMarker(13))
        };

        private static readonly MarkerColor[,] TopLeftMarker =
        {
            { M, C, Y, M },
            { C, Y, M, C },
            { Y, M, C, Y }
        };

        private static readonly MarkerColor[,] BottomRightMarker =
        {
            { Y, C, M, Y },
            { M, Y, C, M },
            { C, M, Y, C }
        };

        public static bool TryDetect(
            Bitmap bitmap,
            ISet<int>? enabledSlots,
            ISet<string>? enabledItemTypes,
            out InventoryMarkerDetection detection)
        {
            detection = new InventoryMarkerDetection();
            if (bitmap == null || bitmap.Width < GuiWidth || bitmap.Height < GuiHeight)
                return false;

            using var pixels = new PixelReader(bitmap);
            bool hasGuiMarkers = TryFindLayoutFromGuiMarkers(pixels, out InventoryMarkerLayout layout);
            bool hasSlotGridLayout = false;
            if (!hasGuiMarkers)
            {
                hasSlotGridLayout = TryFindLayoutFromBlazingSlotGrid(pixels, out layout);
                if (!hasSlotGridLayout && !TryFindLayoutFromItemMarkers(pixels, out layout))
                    return false;
            }
            bool supportsFullInventoryScan = hasGuiMarkers || hasSlotGridLayout;

            var markedSlots = new List<int>();
            var detectedItems = new List<DetectedInventoryItem>();
            var allNonCobblestoneSlots = new List<int>();
            var allNonCobblestoneItems = new List<DetectedInventoryItem>();
            var unknownMarkerSlots = new List<int>();
            var fullCobblestoneSlots = new List<int>();
            for (int slot = 0; slot < 27; slot++)
            {
                int column = slot % 9;
                int row = slot / 9;
                int itemX = layout.Left + (SlotStartX + column * SlotStep) * layout.Scale;
                int itemY = layout.Top + (SlotStartY + row * SlotStep) * layout.Scale;
                int detectedQuantity = 0;
                bool looksLikeCobblestone = LooksLikeCobblestone(pixels, itemX, itemY, layout.Scale);
                bool looksLikeProtectedCobblestone = LooksLikeCobblestone(
                    pixels,
                    itemX,
                    itemY,
                    layout.Scale,
                    minimumNeutralTexturePixels: 180,
                    minimumDarkTexturePixels: 80);
                int markerX = layout.Left + (SlotStartX + column * SlotStep + ItemMarkerX) * layout.Scale;
                int markerY = layout.Top + (SlotStartY + row * SlotStep + ItemMarkerY) * layout.Scale;
                bool hasKnownItem = TryMatchItemMarker(pixels, markerX, markerY, layout.Scale, out string? itemId);
                if (!hasKnownItem
                    && TryMatchSolidBlockItem(pixels, itemX, itemY, layout.Scale, out string solidBlockItemId))
                {
                    hasKnownItem = true;
                    itemId = solidBlockItemId;
                }

                bool hasLegacyMarker = !hasKnownItem
                    && MatchesScaledPatternNear(pixels, markerX, markerY, layout.Scale, LegacyItemMarker);

                // A typed marker or a dedicated solid block color is authoritative.
                // Obsidian must never be mistaken for protected cobblestone.
                if (hasKnownItem)
                {
                    looksLikeCobblestone = false;
                    looksLikeProtectedCobblestone = false;
                }

                if (looksLikeCobblestone && MatchesStackCount64(pixels, itemX, itemY, layout.Scale))
                {
                    fullCobblestoneSlots.Add(slot);
                    detectedQuantity = 64;
                }

                if (enabledSlots != null && !enabledSlots.Contains(slot))
                    continue;

                if (hasKnownItem)
                {
                    if (enabledItemTypes == null || enabledItemTypes.Contains(itemId))
                    {
                        if (detectedQuantity == 0)
                            detectedQuantity = DetectStackCount(pixels, itemX, itemY, layout.Scale);

                        markedSlots.Add(slot);
                        detectedItems.Add(new DetectedInventoryItem(slot, itemId, detectedQuantity));
                    }
                }
                else if (hasLegacyMarker)
                {
                    unknownMarkerSlots.Add(slot);
                }

                bool occupied = hasKnownItem
                    || hasLegacyMarker
                    || (supportsFullInventoryScan && LooksLikeOccupiedSlot(pixels, itemX, itemY, layout.Scale));
                if (occupied && !looksLikeProtectedCobblestone)
                {
                    if (detectedQuantity == 0)
                        detectedQuantity = DetectStackCount(pixels, itemX, itemY, layout.Scale);

                    allNonCobblestoneSlots.Add(slot);
                    allNonCobblestoneItems.Add(new DetectedInventoryItem(
                        slot,
                        hasKnownItem ? itemId : "other",
                        detectedQuantity));
                }
            }

            detection = new InventoryMarkerDetection
            {
                Layout = layout,
                HasGuiMarkers = hasGuiMarkers,
                SupportsFullInventoryScan = supportsFullInventoryScan,
                MarkedSlots = markedSlots,
                Items = detectedItems,
                AllNonCobblestoneSlots = allNonCobblestoneSlots,
                AllNonCobblestoneItems = allNonCobblestoneItems,
                UnknownMarkerSlots = unknownMarkerSlots,
                FullCobblestoneSlots = fullCobblestoneSlots
            };
            return true;
        }

        public static bool ContainsMarkedItem(
            Bitmap bitmap,
            InventoryMarkerLayout layout,
            string itemId,
            bool includeHotbar)
        {
            if (bitmap == null || string.IsNullOrWhiteSpace(itemId) || layout.Scale <= 0)
                return false;

            using var pixels = new PixelReader(bitmap);
            int slotCount = includeHotbar ? 36 : 27;
            for (int slot = 0; slot < slotCount; slot++)
            {
                int column = slot % 9;
                int logicalY = slot < 27
                    ? SlotStartY + (slot / 9) * SlotStep
                    : HotbarSlotStartY;
                int markerX = layout.Left + (SlotStartX + column * SlotStep + ItemMarkerX) * layout.Scale;
                int markerY = layout.Top + (logicalY + ItemMarkerY) * layout.Scale;
                if (TryMatchItemMarker(pixels, markerX, markerY, layout.Scale, out string detectedItemId)
                    && string.Equals(detectedItemId, itemId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindLayoutFromBlazingSlotGrid(PixelReader pixels, out InventoryMarkerLayout layout)
        {
            layout = default;
            int bestBorderScore = 0;
            int bestInteriorScore = 0;

            for (int scale = 1; scale <= MaximumGuiScale; scale++)
            {
                int left = (pixels.Width - BlazingGuiWidth * scale) / 2;
                int top = (pixels.Height - BlazingGuiHeight * scale) / 2;
                int firstSlotX = left + SlotStartX * scale;
                int firstSlotY = top + SlotStartY * scale;
                int lastSlotRight = firstSlotX + (8 * SlotStep + 17) * scale;
                int lastSlotBottom = firstSlotY + (2 * SlotStep + 17) * scale;
                if (firstSlotX < 0 || firstSlotY < 0
                    || lastSlotRight >= pixels.Width || lastSlotBottom >= pixels.Height)
                {
                    continue;
                }

                int borderScore = 0;
                int interiorScore = 0;
                for (int slot = 0; slot < 27; slot++)
                {
                    int column = slot % 9;
                    int row = slot / 9;
                    int itemX = left + (SlotStartX + column * SlotStep) * scale;
                    int itemY = top + (SlotStartY + row * SlotStep) * scale;

                    if (pixels.IsNeutralInRange(
                        itemX + 17 * scale + scale / 2,
                        itemY + scale + scale / 2,
                        35,
                        85,
                        6))
                    {
                        borderScore++;
                    }
                    if (pixels.IsNeutralInRange(
                        itemX + scale + scale / 2,
                        itemY + 17 * scale + scale / 2,
                        35,
                        85,
                        6))
                    {
                        borderScore++;
                    }

                    foreach ((int X, int Y) offset in new[] { (1, 1), (14, 1), (1, 14), (14, 14) })
                    {
                        if (pixels.IsNeutralInRange(
                            itemX + offset.X * scale + scale / 2,
                            itemY + offset.Y * scale + scale / 2,
                            100,
                            190,
                            6))
                        {
                            interiorScore++;
                        }
                    }
                }

                if (borderScore < MinimumBlazingDarkBorders || interiorScore < 72)
                    continue;
                if (borderScore < bestBorderScore
                    || (borderScore == bestBorderScore && interiorScore <= bestInteriorScore))
                {
                    continue;
                }

                bestBorderScore = borderScore;
                bestInteriorScore = interiorScore;
                layout = new InventoryMarkerLayout(left, top, scale);
            }

            return bestBorderScore >= MinimumBlazingDarkBorders;
        }

        private static bool LooksLikeCobblestone(
            PixelReader pixels,
            int itemX,
            int itemY,
            int scale,
            int minimumNeutralTexturePixels = 105,
            int minimumDarkTexturePixels = 45)
        {
            int neutralTexturePixels = 0;
            int coloredTexturePixels = 0;
            int darkTexturePixels = 0;
            int protectedPinkPixels = 0;
            int sampleOffset = scale / 2;

            for (int logicalY = 0; logicalY < 16; logicalY++)
            {
                for (int logicalX = 0; logicalX < 16; logicalX++)
                {
                    if (!pixels.TryGetColor(
                        itemX + logicalX * scale + sampleOffset,
                        itemY + logicalY * scale + sampleOffset,
                        out MarkerColor color))
                    {
                        continue;
                    }

                    int maximum = Math.Max(color.R, Math.Max(color.G, color.B));
                    int minimum = Math.Min(color.R, Math.Min(color.G, color.B));
                    int spread = maximum - minimum;
                    int intensity = (color.R + color.G + color.B) / 3;
                    bool differsFromSlotBackground = Math.Abs(intensity - 139) >= 11;

                    // The updated helper texture uses solid RGB 255,174,201 for
                    // cobblestone. Minecraft shades the three block faces while
                    // rendering the inventory model, so match its pink hue rather
                    // than one exact RGB value.
                    if (color.R >= 120
                        && color.G >= 65
                        && color.B >= 85
                        && color.R - color.G >= 25
                        && color.B - color.G >= 8
                        && color.R - color.B >= 12)
                    {
                        protectedPinkPixels++;
                    }

                    if (spread <= 16 && differsFromSlotBackground)
                    {
                        neutralTexturePixels++;
                        if (intensity < 95)
                            darkTexturePixels++;
                    }
                    else if (spread > 28 && differsFromSlotBackground)
                    {
                        coloredTexturePixels++;
                    }
                }
            }

            // A cobblestone block fills most of the 16x16 item area and is almost entirely neutral gray.
            // Enchanted/mossy CobbleX variants contain enough purple/green to fail this input check.
            bool looksLikeLegacyGrayCobblestone = neutralTexturePixels >= minimumNeutralTexturePixels
                && darkTexturePixels >= minimumDarkTexturePixels
                && coloredTexturePixels <= 28;

            // The stricter protection pass asks for 180 neutral pixels. Its pink
            // equivalent is deliberately conservative, but still allows for the
            // lighting applied to a rendered 3D block icon.
            int minimumProtectedPinkPixels = minimumNeutralTexturePixels >= 180 ? 70 : 45;
            bool looksLikeUpdatedPinkCobblestone = protectedPinkPixels >= minimumProtectedPinkPixels;
            return looksLikeLegacyGrayCobblestone || looksLikeUpdatedPinkCobblestone;
        }

        private static bool LooksLikeOccupiedSlot(PixelReader pixels, int itemX, int itemY, int scale)
        {
            int itemPixels = 0;
            int sampleOffset = scale / 2;

            for (int logicalY = 0; logicalY < 16; logicalY++)
            {
                for (int logicalX = 0; logicalX < 16; logicalX++)
                {
                    if (!pixels.TryGetColor(
                        itemX + logicalX * scale + sampleOffset,
                        itemY + logicalY * scale + sampleOffset,
                        out MarkerColor color))
                    {
                        continue;
                    }

                    int maximum = Math.Max(color.R, Math.Max(color.G, color.B));
                    int minimum = Math.Min(color.R, Math.Min(color.G, color.B));
                    int spread = maximum - minimum;
                    int intensity = (color.R + color.G + color.B) / 3;
                    if (spread > 14 || Math.Abs(intensity - 139) > 14)
                    {
                        itemPixels++;
                        if (itemPixels >= 20)
                            return true;
                    }
                }
            }

            return false;
        }

        private static bool TryMatchSolidBlockItem(
            PixelReader pixels,
            int itemX,
            int itemY,
            int scale,
            out string itemId)
        {
            int obsidianPixels = 0;
            int sandPixels = 0;
            int goldBlockPixels = 0;
            int ironBlockPixels = 0;
            int emeraldBlockPixels = 0;
            int sampleOffset = scale / 2;

            for (int logicalY = 0; logicalY < 16; logicalY++)
            {
                for (int logicalX = 0; logicalX < 16; logicalX++)
                {
                    if (!pixels.TryGetColor(
                        itemX + logicalX * scale + sampleOffset,
                        itemY + logicalY * scale + sampleOffset,
                        out MarkerColor color))
                    {
                        continue;
                    }

                    // Source texture RGB 60,48,86. The broad ranges account for
                    // lighting on the three faces of Minecraft's inventory block.
                    if (color.B >= 35
                        && color.B <= 125
                        && color.R >= 22
                        && color.R <= 95
                        && color.G >= 16
                        && color.G <= 80
                        && color.B - color.R >= 12
                        && color.B - color.G >= 18
                        && color.R - color.G >= 5
                        && color.R - color.G <= 25)
                    {
                        obsidianPixels++;
                    }

                    // Source texture RGB 255,201,14, also matched after face shading.
                    if (color.R >= 125
                        && color.G >= 85
                        && color.B <= 65
                        && color.R - color.G >= 28
                        && color.R - color.G <= 75
                        && color.G - color.B >= 70)
                    {
                        sandPixels++;
                    }

                    // Source texture RGB 255,242,0. It stays distinctly more
                    // yellow than sand after Minecraft shades the block faces.
                    if (color.R >= 125
                        && color.G >= 115
                        && color.B <= 50
                        && color.R - color.G >= 3
                        && color.R - color.G <= 25
                        && color.G - color.B >= 100)
                    {
                        goldBlockPixels++;
                    }

                    // Source texture RGB 232,232,232. The normal slot background
                    // is close to RGB 139,139,139, so only the two brighter faces
                    // are counted. This prevents empty slots from becoming iron.
                    int maximum = Math.Max(color.R, Math.Max(color.G, color.B));
                    int minimum = Math.Min(color.R, Math.Min(color.G, color.B));
                    int intensity = (color.R + color.G + color.B) / 3;
                    if (maximum - minimum <= 8
                        && intensity >= 160
                        && intensity <= 248)
                    {
                        ironBlockPixels++;
                    }

                    // Source texture RGB 131,237,161, matched across the three
                    // differently shaded faces of the inventory block model.
                    if (color.G >= 100
                        && color.R >= 45
                        && color.B >= 55
                        && color.G - color.R >= 45
                        && color.G - color.B >= 35
                        && color.B - color.R >= 15
                        && color.B - color.R <= 50)
                    {
                        emeraldBlockPixels++;
                    }
                }
            }

            const int minimumIronBlockPixels = 30;
            string bestItemId = string.Empty;
            int bestPixelCount = 0;

            void Consider(string candidateItemId, int pixelCount, int requiredPixelCount = 45)
            {
                if (pixelCount >= requiredPixelCount && pixelCount > bestPixelCount)
                {
                    bestItemId = candidateItemId;
                    bestPixelCount = pixelCount;
                }
            }

            Consider("obsidian", obsidianPixels);
            Consider("sand", sandPixels);
            Consider("gold_block", goldBlockPixels);
            Consider("iron_block", ironBlockPixels, minimumIronBlockPixels);
            Consider("emerald_block", emeraldBlockPixels);

            itemId = bestItemId;
            return bestPixelCount > 0;
        }

        private static bool MatchesStackCount64(PixelReader pixels, int itemX, int itemY, int scale)
        {
            // Vanilla 1.8.8 draws the two five-pixel-wide glyphs at x+5, y+9.
            // Search a tiny physical-pixel radius because window capture can be offset by one pixel.
            int minimumBrightPixels = 27;
            for (int physicalOffsetY = -2; physicalOffsetY <= 2; physicalOffsetY++)
            {
                for (int physicalOffsetX = -2; physicalOffsetX <= 2; physicalOffsetX++)
                {
                    int brightPixels = 0;
                    int expectedPixels = 0;
                    for (int row = 0; row < StackCount64Glyphs.Length; row++)
                    {
                        string maskRow = StackCount64Glyphs[row];
                        for (int column = 0; column < maskRow.Length; column++)
                        {
                            if (maskRow[column] != '#')
                                continue;

                            expectedPixels++;
                            int x = itemX + (5 + column) * scale + scale / 2 + physicalOffsetX;
                            int y = itemY + (9 + row) * scale + scale / 2 + physicalOffsetY;
                            if (pixels.IsNeutralInRange(x, y, 190, 255, 24))
                                brightPixels++;
                        }
                    }

                    if (expectedPixels >= minimumBrightPixels && brightPixels >= minimumBrightPixels)
                        return true;
                }
            }

            return false;
        }

        private static int DetectStackCount(PixelReader pixels, int itemX, int itemY, int scale)
        {
            if (MatchesStackCount64(pixels, itemX, itemY, scale))
                return 64;

            int bestCount = 1;
            int bestScore = int.MinValue;
            double bestHitRatio = 0;
            for (int count = 2; count < 64; count++)
            {
                string digits = count.ToString(CultureInfo.InvariantCulture);
                int logicalStartX = 17 - digits.Length * 6;
                int logicalWidth = digits.Length * 6 - 1;

                for (int physicalOffsetY = -2; physicalOffsetY <= 2; physicalOffsetY++)
                {
                    for (int physicalOffsetX = -2; physicalOffsetX <= 2; physicalOffsetX++)
                    {
                        int expectedPixels = 0;
                        int matchedPixels = 0;
                        int unexpectedBrightPixels = 0;

                        for (int row = 0; row < 7; row++)
                        {
                            for (int column = 0; column < logicalWidth; column++)
                            {
                                bool expected = IsExpectedStackCountPixel(digits, row, column);
                                int x = itemX + (logicalStartX + column) * scale + scale / 2 + physicalOffsetX;
                                int y = itemY + (9 + row) * scale + scale / 2 + physicalOffsetY;
                                bool bright = pixels.IsNeutralInRange(x, y, 190, 255, 24);

                                if (expected)
                                {
                                    expectedPixels++;
                                    if (bright)
                                        matchedPixels++;
                                }
                                else if (bright)
                                {
                                    unexpectedBrightPixels++;
                                }
                            }
                        }

                        if (expectedPixels == 0)
                            continue;

                        double hitRatio = matchedPixels / (double)expectedPixels;
                        int score = matchedPixels * 5
                            - (expectedPixels - matchedPixels) * 6
                            - unexpectedBrightPixels * 2;
                        if (score > bestScore
                            || (score == bestScore && hitRatio > bestHitRatio))
                        {
                            bestHitRatio = hitRatio;
                            bestScore = score;
                            bestCount = count;
                        }
                    }
                }
            }

            // No number is drawn for a single item. A fairly strict threshold prevents
            // bright pixels in an item texture from being mistaken for a stack label.
            return bestHitRatio >= 0.78 && bestScore > 0 ? bestCount : 1;
        }

        private static bool IsExpectedStackCountPixel(string digits, int row, int combinedColumn)
        {
            int digitIndex = combinedColumn / 6;
            int columnInDigit = combinedColumn % 6;
            if (digitIndex < 0 || digitIndex >= digits.Length || columnInDigit >= 5)
                return false;

            int digit = digits[digitIndex] - '0';
            return digit >= 0
                && digit < StackCountDigitGlyphs.Length
                && StackCountDigitGlyphs[digit][row][columnInDigit] == '#';
        }

        private static bool TryMatchItemMarker(
            PixelReader pixels,
            int markerX,
            int markerY,
            int scale,
            out string itemId)
        {
            foreach (ItemMarkerDefinition definition in ItemMarkers)
            {
                if (MatchesScaledPatternNear(pixels, markerX, markerY, scale, definition.Pattern))
                {
                    itemId = definition.Id;
                    return true;
                }
            }

            itemId = string.Empty;
            return false;
        }

        private static MarkerColor[,] BuildItemMarker(int markerCode)
        {
            var marker = new MarkerColor[3, 3];
            marker[0, 0] = M;
            marker[0, 1] = C;
            marker[1, 0] = C;
            marker[1, 1] = Y;

            (int Row, int Column)[] codeCells =
            {
                (0, 2),
                (1, 2),
                (2, 0),
                (2, 1),
                (2, 2)
            };
            foreach ((int row, int column) in codeCells)
            {
                marker[row, column] = MarkerCodeColors[markerCode % MarkerCodeColors.Length];
                markerCode /= MarkerCodeColors.Length;
            }

            return marker;
        }

        private static bool TryFindLayoutFromGuiMarkers(PixelReader pixels, out InventoryMarkerLayout layout)
        {
            layout = default;

            for (int scale = 1; scale <= MaximumGuiScale; scale++)
            {
                if (GuiWidth * scale > pixels.Width || GuiHeight * scale > pixels.Height)
                    break;

                int expectedLeft = (pixels.Width - GuiWidth * scale) / 2;
                int expectedTop = (pixels.Height - GuiHeight * scale) / 2;
                int searchRadius = Math.Max(8, scale * 2);

                for (int top = expectedTop - searchRadius; top <= expectedTop + searchRadius; top++)
                {
                    for (int left = expectedLeft - searchRadius; left <= expectedLeft + searchRadius; left++)
                    {
                        int topLeftX = left + TopLeftMarkerX * scale;
                        int topLeftY = top + TopLeftMarkerY * scale;
                        if (!MatchesScaledPattern(pixels, topLeftX, topLeftY, scale, TopLeftMarker))
                            continue;

                        int bottomRightX = left + BottomRightMarkerX * scale;
                        int bottomRightY = top + BottomRightMarkerY * scale;
                        if (!MatchesScaledPattern(pixels, bottomRightX, bottomRightY, scale, BottomRightMarker))
                            continue;

                        layout = new InventoryMarkerLayout(left, top, scale);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryFindLayoutFromItemMarkers(PixelReader pixels, out InventoryMarkerLayout layout)
        {
            layout = default;
            int bestScore = 0;
            long bestCenterDistanceSquared = long.MaxValue;

            for (int scale = 1; scale <= MaximumGuiScale; scale++)
            {
                if (GuiWidth * scale > pixels.Width || GuiHeight * scale > pixels.Height)
                    break;

                List<Point> markers = FindSolidItemMarkers(pixels, scale);
                if (markers.Count < MinimumFallbackMarkers)
                    continue;

                int step = SlotStep * scale;
                int positionTolerance = Math.Max(2, scale / 2);
                foreach (Point marker in markers)
                {
                    for (int assumedRow = 0; assumedRow < 3; assumedRow++)
                    {
                        for (int assumedColumn = 0; assumedColumn < 9; assumedColumn++)
                        {
                            int markerGridLeft = marker.X - assumedColumn * step;
                            int markerGridTop = marker.Y - assumedRow * step;
                            int score = CountMarkersOnGrid(
                                markers,
                                markerGridLeft,
                                markerGridTop,
                                step,
                                positionTolerance);
                            if (score < MinimumFallbackMarkers)
                                continue;

                            long centerX2 = (markerGridLeft + 4 * step) * 2L;
                            long centerY2 = (markerGridTop + step) * 2L;
                            long deltaX2 = centerX2 - pixels.Width;
                            long deltaY2 = centerY2 - pixels.Height;
                            long centerDistanceSquared = deltaX2 * deltaX2 + deltaY2 * deltaY2;

                            if (score < bestScore
                                || (score == bestScore && centerDistanceSquared >= bestCenterDistanceSquared))
                            {
                                continue;
                            }

                            bestScore = score;
                            bestCenterDistanceSquared = centerDistanceSquared;
                            int left = markerGridLeft - (SlotStartX + ItemMarkerX) * scale;
                            int top = markerGridTop - (SlotStartY + ItemMarkerY) * scale;
                            layout = new InventoryMarkerLayout(left, top, scale);
                        }
                    }
                }
            }

            return bestScore >= MinimumFallbackMarkers;
        }

        private static List<Point> FindSolidItemMarkers(PixelReader pixels, int scale)
        {
            var markers = new List<Point>();
            int patternWidth = MarkerLocator.GetLength(1) * scale;
            int patternHeight = MarkerLocator.GetLength(0) * scale;

            for (int y = 0; y <= pixels.Height - patternHeight; y++)
            {
                for (int x = 0; x <= pixels.Width - patternWidth; x++)
                {
                    if (MatchesSolidScaledPattern(pixels, x, y, scale, MarkerLocator))
                        markers.Add(new Point(x, y));
                }
            }

            return markers;
        }

        private static int CountMarkersOnGrid(
            IReadOnlyList<Point> markers,
            int gridLeft,
            int gridTop,
            int step,
            int tolerance)
        {
            int score = 0;
            foreach (Point marker in markers)
            {
                bool isOnGrid = false;
                for (int row = 0; row < 3 && !isOnGrid; row++)
                {
                    int expectedY = gridTop + row * step;
                    if (Math.Abs(marker.Y - expectedY) > tolerance)
                        continue;

                    for (int column = 0; column < 9; column++)
                    {
                        int expectedX = gridLeft + column * step;
                        if (Math.Abs(marker.X - expectedX) <= tolerance)
                        {
                            isOnGrid = true;
                            break;
                        }
                    }
                }

                if (isOnGrid)
                    score++;
            }

            return score;
        }

        private static bool MatchesScaledPatternNear(
            PixelReader pixels,
            int startX,
            int startY,
            int scale,
            MarkerColor[,] pattern)
        {
            int radius = Math.Max(2, scale / 2);
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    if (MatchesScaledPattern(pixels, startX + offsetX, startY + offsetY, scale, pattern))
                        return true;
                }
            }

            return false;
        }

        private static bool MatchesSolidScaledPattern(
            PixelReader pixels,
            int startX,
            int startY,
            int scale,
            MarkerColor[,] pattern)
        {
            int rows = pattern.GetLength(0);
            int columns = pattern.GetLength(1);

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    for (int cellY = 0; cellY < scale; cellY++)
                    {
                        for (int cellX = 0; cellX < scale; cellX++)
                        {
                            int x = startX + column * scale + cellX;
                            int y = startY + row * scale + cellY;
                            if (!pixels.Matches(x, y, pattern[row, column], ColorTolerance))
                                return false;
                        }
                    }
                }
            }

            return true;
        }

        private static bool MatchesScaledPattern(
            PixelReader pixels,
            int startX,
            int startY,
            int scale,
            MarkerColor[,] pattern)
        {
            int rows = pattern.GetLength(0);
            int columns = pattern.GetLength(1);
            int sampleOffset = scale / 2;

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int x = startX + column * scale + sampleOffset;
                    int y = startY + row * scale + sampleOffset;
                    if (!pixels.Matches(x, y, pattern[row, column], ColorTolerance))
                        return false;
                }
            }

            return true;
        }

        private readonly record struct MarkerColor(byte R, byte G, byte B);
        private readonly record struct ItemMarkerDefinition(string Id, MarkerColor[,] Pattern);

        private sealed class PixelReader : IDisposable
        {
            private readonly Bitmap _bitmap;
            private readonly BitmapData _bitmapData;
            private readonly byte[] _pixels;
            private readonly int _stride;
            private bool _disposed;

            public PixelReader(Bitmap bitmap)
            {
                _bitmap = bitmap;
                Width = bitmap.Width;
                Height = bitmap.Height;
                _bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, Width, Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppArgb);
                _stride = Math.Abs(_bitmapData.Stride);
                _pixels = new byte[_stride * Height];
                Marshal.Copy(_bitmapData.Scan0, _pixels, 0, _pixels.Length);
            }

            public int Width { get; }
            public int Height { get; }

            public bool Matches(int x, int y, MarkerColor expected, int tolerance)
            {
                if (!TryGetColor(x, y, out MarkerColor actual))
                    return false;

                return Math.Abs(actual.R - expected.R) <= tolerance
                    && Math.Abs(actual.G - expected.G) <= tolerance
                    && Math.Abs(actual.B - expected.B) <= tolerance;
            }

            public bool IsNeutralInRange(
                int x,
                int y,
                int minimumIntensity,
                int maximumIntensity,
                int maximumSpread)
            {
                if (!TryGetColor(x, y, out MarkerColor color))
                    return false;

                int maximum = Math.Max(color.R, Math.Max(color.G, color.B));
                int minimum = Math.Min(color.R, Math.Min(color.G, color.B));
                int intensity = (color.R + color.G + color.B) / 3;
                return maximum - minimum <= maximumSpread
                    && intensity >= minimumIntensity
                    && intensity <= maximumIntensity;
            }

            public bool TryGetColor(int x, int y, out MarkerColor color)
            {
                color = default;
                if (x < 0 || y < 0 || x >= Width || y >= Height)
                    return false;

                int storedY = _bitmapData.Stride >= 0 ? y : Height - 1 - y;
                int offset = storedY * _stride + x * 4;
                byte blue = _pixels[offset];
                byte green = _pixels[offset + 1];
                byte red = _pixels[offset + 2];
                color = new MarkerColor(red, green, blue);
                return true;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _bitmap.UnlockBits(_bitmapData);
                _disposed = true;
            }
        }
    }
}
