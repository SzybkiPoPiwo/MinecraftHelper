using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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
        public IReadOnlyList<int> MarkedSlots { get; init; } = Array.Empty<int>();
        public IReadOnlyList<DetectedInventoryItem> Items { get; init; } = Array.Empty<DetectedInventoryItem>();
        public IReadOnlyList<int> UnknownMarkerSlots { get; init; } = Array.Empty<int>();
        public IReadOnlyList<int> FullCobblestoneSlots { get; init; } = Array.Empty<int>();
    }

    internal readonly record struct DetectedInventoryItem(int Slot, string ItemId);

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
            new ItemMarkerDefinition("redstone", BuildItemMarker(12))
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
            if (!TryFindLayoutFromGuiMarkers(pixels, out InventoryMarkerLayout layout)
                && !TryFindLayoutFromBlazingSlotGrid(pixels, out layout)
                && !TryFindLayoutFromItemMarkers(pixels, out layout))
                return false;

            var markedSlots = new List<int>();
            var detectedItems = new List<DetectedInventoryItem>();
            var unknownMarkerSlots = new List<int>();
            var fullCobblestoneSlots = new List<int>();
            for (int slot = 0; slot < 27; slot++)
            {
                int column = slot % 9;
                int row = slot / 9;
                int itemX = layout.Left + (SlotStartX + column * SlotStep) * layout.Scale;
                int itemY = layout.Top + (SlotStartY + row * SlotStep) * layout.Scale;
                if (LooksLikeCobblestone(pixels, itemX, itemY, layout.Scale)
                    && MatchesStackCount64(pixels, itemX, itemY, layout.Scale))
                {
                    fullCobblestoneSlots.Add(slot);
                }

                if (enabledSlots != null && !enabledSlots.Contains(slot))
                    continue;

                int markerX = layout.Left + (SlotStartX + column * SlotStep + ItemMarkerX) * layout.Scale;
                int markerY = layout.Top + (SlotStartY + row * SlotStep + ItemMarkerY) * layout.Scale;
                if (TryMatchItemMarker(pixels, markerX, markerY, layout.Scale, out string? itemId))
                {
                    if (enabledItemTypes == null || enabledItemTypes.Contains(itemId))
                    {
                        markedSlots.Add(slot);
                        detectedItems.Add(new DetectedInventoryItem(slot, itemId));
                    }
                }
                else if (MatchesScaledPatternNear(pixels, markerX, markerY, layout.Scale, LegacyItemMarker))
                {
                    unknownMarkerSlots.Add(slot);
                }
            }

            detection = new InventoryMarkerDetection
            {
                Layout = layout,
                MarkedSlots = markedSlots,
                Items = detectedItems,
                UnknownMarkerSlots = unknownMarkerSlots,
                FullCobblestoneSlots = fullCobblestoneSlots
            };
            return true;
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

        private static bool LooksLikeCobblestone(PixelReader pixels, int itemX, int itemY, int scale)
        {
            int neutralTexturePixels = 0;
            int coloredTexturePixels = 0;
            int darkTexturePixels = 0;
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
            return neutralTexturePixels >= 105
                && darkTexturePixels >= 45
                && coloredTexturePixels <= 28;
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
