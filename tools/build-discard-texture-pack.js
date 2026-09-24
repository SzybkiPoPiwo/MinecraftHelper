"use strict";

const fs = require("fs");
const path = require("path");
const zlib = require("zlib");

const workspace = path.resolve(__dirname, "..");
const sourceRoot = path.join(workspace, "Old_Default_1.8.8");
const outputRoot = path.join(workspace, "MinecraftHelper_AutoEQ_CobbleX_1.8.8");
const packIconSource = path.join(workspace, "MinecraftHelper", "Assets", "pack.png");

const textureRoot = path.join(outputRoot, "assets", "minecraft", "textures");
const itemRoot = path.join(textureRoot, "items");
const blockRoot = path.join(textureRoot, "blocks");
const modelRoot = path.join(outputRoot, "assets", "minecraft", "models", "item");

const COLORS = {
    magenta: [255, 0, 255, 255],
    cyan: [0, 255, 255, 255],
    yellow: [255, 255, 0, 255]
};

const MARKER_CODE_COLORS = [COLORS.magenta, COLORS.cyan, COLORS.yellow];

// Every item shares the uncommon 2x2 locator in the upper-left corner. The
// remaining five cells encode its type in base 3, allowing the app to decide
// which resources should be discarded and which must stay in the inventory.
function buildItemMarker(markerCode) {
    const marker = [
        [COLORS.magenta, COLORS.cyan, COLORS.magenta],
        [COLORS.cyan, COLORS.yellow, COLORS.magenta],
        [COLORS.magenta, COLORS.magenta, COLORS.magenta]
    ];
    const codeCells = [[0, 2], [1, 2], [2, 0], [2, 1], [2, 2]];
    for (const [row, column] of codeCells) {
        marker[row][column] = MARKER_CODE_COLORS[markerCode % MARKER_CODE_COLORS.length];
        markerCode = Math.floor(markerCode / MARKER_CODE_COLORS.length);
    }
    return marker;
}

const GUI_TOP_LEFT_MARKER = [
    [COLORS.magenta, COLORS.cyan, COLORS.yellow, COLORS.magenta],
    [COLORS.cyan, COLORS.yellow, COLORS.magenta, COLORS.cyan],
    [COLORS.yellow, COLORS.magenta, COLORS.cyan, COLORS.yellow]
];

const GUI_BOTTOM_RIGHT_MARKER = [
    [COLORS.yellow, COLORS.cyan, COLORS.magenta, COLORS.yellow],
    [COLORS.magenta, COLORS.yellow, COLORS.cyan, COLORS.magenta],
    [COLORS.cyan, COLORS.magenta, COLORS.yellow, COLORS.cyan]
];

const GUI_WIDTH = 176;
const GUI_HEIGHT = 166;
const GUI_TOP_LEFT_MARKER_X = 1;
const GUI_TOP_LEFT_MARKER_Y = 1;
const GUI_BOTTOM_RIGHT_MARKER_X = 171;
const GUI_BOTTOM_RIGHT_MARKER_Y = 162;
const ITEM_MARKER_X = 13;
const ITEM_MARKER_Y = 0;

const directItemTextures = [
    { id: "diamond", file: "diamond.png", label: "diament", markerCode: 0 },
    { id: "gold_ingot", file: "gold_ingot.png", label: "sztabka zlota", markerCode: 1 },
    { id: "iron_ingot", file: "iron_ingot.png", label: "sztabka zelaza", markerCode: 2 },
    { id: "apple", file: "apple.png", label: "jablko", markerCode: 4 },
    { id: "gunpowder", file: "gunpowder.png", label: "proch", markerCode: 6 },
    { id: "emerald", file: "emerald.png", label: "emerald", markerCode: 7 },
    { id: "coal", file: "coal.png", label: "wegiel", markerCode: 8 },
    { id: "quartz", file: "quartz.png", label: "kwarc", markerCode: 9 },
    { id: "book", file: "book_normal.png", label: "zwykla ksiazka", markerCode: 10 },
    { id: "ender_pearl", file: "ender_pearl.png", label: "ender perla", markerCode: 11 },
    { id: "redstone", file: "redstone_dust.png", label: "redstone", markerCode: 12 }
];

const blockItems = [
    { id: "obsidian", source: "obsidian.png", output: "mh_discard_obsidian.png", label: "obsydian", markerCode: 3 },
    { id: "sand", source: "sand.png", output: "mh_discard_sand.png", label: "piasek", markerCode: 5 }
];

function main() {
    if (!fs.existsSync(sourceRoot))
        throw new Error(`Brak paczki zrodlowej: ${sourceRoot}`);

    fs.cpSync(sourceRoot, outputRoot, { recursive: true, force: true });

    for (const item of directItemTextures) {
        const filePath = path.join(itemRoot, item.file);
        markPng(filePath, ITEM_MARKER_X, ITEM_MARKER_Y, buildItemMarker(item.markerCode));
    }

    const standardItemModel = JSON.parse(fs.readFileSync(path.join(modelRoot, "diamond.json"), "utf8"));
    for (const item of blockItems) {
        const sourcePath = path.join(blockRoot, item.source);
        const outputPath = path.join(itemRoot, item.output);
        const image = readPng(sourcePath);
        drawPattern(image, ITEM_MARKER_X, ITEM_MARKER_Y, buildItemMarker(item.markerCode));
        writePng(outputPath, image);

        const model = {
            parent: "builtin/generated",
            textures: { layer0: `items/${path.basename(item.output, ".png")}` },
            display: standardItemModel.display
        };
        fs.writeFileSync(path.join(modelRoot, `${item.id}.json`), `${JSON.stringify(model, null, 4)}\n`, "utf8");
    }

    const inventoryPath = path.join(textureRoot, "gui", "container", "inventory.png");
    const inventory = readPng(inventoryPath);
    if (inventory.width < GUI_WIDTH || inventory.height < GUI_HEIGHT)
        throw new Error(`inventory.png ma nieoczekiwany rozmiar ${inventory.width}x${inventory.height}`);
    drawPattern(inventory, GUI_TOP_LEFT_MARKER_X, GUI_TOP_LEFT_MARKER_Y, GUI_TOP_LEFT_MARKER);
    drawPattern(inventory, GUI_BOTTOM_RIGHT_MARKER_X, GUI_BOTTOM_RIGHT_MARKER_Y, GUI_BOTTOM_RIGHT_MARKER);
    writePng(inventoryPath, inventory);

    updatePackDescription();
    writePackIcon();
    writeManifest();
    writeReadme();
    verifyOutput();
    writeDetectorFixture();
    writeBlazingDetectorFixture();

    console.log(`Gotowe: ${outputRoot}`);
    console.log(`Oznaczone przedmioty: ${directItemTextures.length + blockItems.length}`);
}

function markPng(filePath, x, y, pattern) {
    const image = readPng(filePath);
    drawPattern(image, x, y, pattern);
    writePng(filePath, image);
}

function drawPattern(image, startX, startY, pattern) {
    for (let y = 0; y < pattern.length; y++) {
        for (let x = 0; x < pattern[y].length; x++) {
            const targetX = startX + x;
            const targetY = startY + y;
            if (targetX < 0 || targetY < 0 || targetX >= image.width || targetY >= image.height)
                throw new Error(`Znacznik wychodzi poza obraz ${image.width}x${image.height}`);
            setPixel(image, targetX, targetY, pattern[y][x]);
        }
    }
}

function updatePackDescription() {
    const filePath = path.join(outputRoot, "pack.mcmeta");
    const metadata = JSON.parse(fs.readFileSync(filePath, "utf8"));
    metadata.pack.description = "MinecraftHelper AutoEQ + CobbleX (1.8.8)";
    fs.writeFileSync(filePath, `${JSON.stringify(metadata, null, 2)}\n`, "utf8");
}

function writePackIcon() {
    if (!fs.existsSync(packIconSource))
        throw new Error(`Brak ikony paczki: ${packIconSource}`);
    fs.copyFileSync(packIconSource, path.join(outputRoot, "pack.png"));
}

function writeManifest() {
    const manifest = {
        format: 1,
        minecraftVersion: "1.8.8",
        gui: {
            width: GUI_WIDTH,
            height: GUI_HEIGHT,
            topLeftMarker: { x: GUI_TOP_LEFT_MARKER_X, y: GUI_TOP_LEFT_MARKER_Y },
            bottomRightMarker: { x: GUI_BOTTOM_RIGHT_MARKER_X, y: GUI_BOTTOM_RIGHT_MARKER_Y }
        },
        itemMarker: { version: 2, x: ITEM_MARKER_X, y: ITEM_MARKER_Y, width: 3, height: 3 },
        discardItems: [
            ...directItemTextures.map(item => ({ id: item.id, label: item.label, markerCode: item.markerCode, texture: `items/${item.file}` })),
            ...blockItems.map(item => ({ id: item.id, label: item.label, markerCode: item.markerCode, texture: `items/${item.output}` }))
        ]
    };

    fs.writeFileSync(
        path.join(outputRoot, "minecraft-helper-markers.json"),
        `${JSON.stringify(manifest, null, 2)}\n`,
        "utf8");
}

function writeReadme() {
    const lines = [
        "MinecraftHelper AutoEQ + CobbleX - paczka dla 1.8.8",
        "",
        "Paczka jest generowana z Old_Default_1.8.8 przez:",
        "  node tools/build-discard-texture-pack.js",
        "",
        "Oznaczone przedmioty:",
        ...directItemTextures.map(item => `- ${item.label}`),
        ...blockItems.map(item => `- ${item.label}`),
        "",
        "Zlotych jablek, wegla drzewnego i innych wariantow ksiazek celowo nie oznaczono.",
        "Kazdy typ ma osobny kod znacznika; wybor typow do wyrzucenia ustawia sie w aplikacji.",
        "Program skanuje tylko 27 glownych slotow ekwipunku; hotbar jest pomijany.",
        "Wymagane sa domyslne klawisze Minecrafta: E (ekwipunek), Q (wyrzucanie) i T (czat)."
    ];
    fs.writeFileSync(path.join(outputRoot, "MINECRAFT_HELPER.txt"), `${lines.join("\r\n")}\r\n`, "utf8");
}

function verifyOutput() {
    const targets = [
        ...directItemTextures.map(item => ({ path: path.join(itemRoot, item.file), markerCode: item.markerCode })),
        ...blockItems.map(item => ({ path: path.join(itemRoot, item.output), markerCode: item.markerCode }))
    ];

    for (const target of targets) {
        const image = readPng(target.path);
        assertPattern(image, ITEM_MARKER_X, ITEM_MARKER_Y, buildItemMarker(target.markerCode), path.basename(target.path));
    }

    const inventory = readPng(path.join(textureRoot, "gui", "container", "inventory.png"));
    assertPattern(inventory, GUI_TOP_LEFT_MARKER_X, GUI_TOP_LEFT_MARKER_Y, GUI_TOP_LEFT_MARKER, "inventory TL");
    assertPattern(inventory, GUI_BOTTOM_RIGHT_MARKER_X, GUI_BOTTOM_RIGHT_MARKER_Y, GUI_BOTTOM_RIGHT_MARKER, "inventory BR");
}

function assertPattern(image, startX, startY, pattern, name) {
    for (let y = 0; y < pattern.length; y++) {
        for (let x = 0; x < pattern[y].length; x++) {
            const actual = getPixel(image, startX + x, startY + y);
            const expected = pattern[y][x];
            if (!actual.every((value, index) => value === expected[index]))
                throw new Error(`Niepoprawny znacznik w ${name}, piksel ${x},${y}`);
        }
    }
}

function writeDetectorFixture() {
    const inventory = readPng(path.join(textureRoot, "gui", "container", "inventory.png"));
    const fixture = createImage(GUI_WIDTH, GUI_HEIGHT, [0, 0, 0, 255]);
    blit(fixture, inventory, 0, 0, GUI_WIDTH, GUI_HEIGHT);

    const itemFiles = [
        ...directItemTextures.map(item => ({ path: path.join(itemRoot, item.file), markerCode: item.markerCode })),
        ...blockItems.map(item => ({ path: path.join(itemRoot, item.output), markerCode: item.markerCode }))
    ];
    for (let index = 0; index < itemFiles.length; index++) {
        const item = readPng(itemFiles[index].path);
        const column = index % 9;
        const row = Math.floor(index / 9);
        blit(fixture, item, 8 + column * 18, 84 + row * 18, 16, 16);
    }

    const scaled = scaleNearest(fixture, 3);
    const fixturePath = path.join(outputRoot, "minecraft-helper-detector-test.png");
    writePng(fixturePath, scaled);

    const detection = detectFixture(scaled);
    if (detection.scale !== 3 || detection.slots.length !== itemFiles.length)
        throw new Error(`Test detektora nie przeszedl: skala=${detection.scale}, sloty=${detection.slots.length}`);
    console.log(`Test detektora OK: skala ${detection.scale}, wykryte sloty ${detection.slots.join(", ")}`);
}

function writeBlazingDetectorFixture() {
    const scale = 3;
    const screen = createImage(1280, 720, [105, 116, 134, 255]);
    const itemFiles = [
        ...directItemTextures.map(item => path.join(itemRoot, item.file)),
        ...blockItems.map(item => path.join(itemRoot, item.output))
    ];
    const firstItemX = 375;
    const firstItemY = 318;
    const slotStep = 18 * scale;

    for (let index = 0; index < itemFiles.length; index++) {
        const item = scaleNearest(readPng(itemFiles[index]), scale);
        const column = index % 9;
        const row = Math.floor(index / 9);
        blit(screen, item, firstItemX + column * slotStep, firstItemY + row * slotStep);
    }

    writePng(path.join(outputRoot, "minecraft-helper-blazing-detector-test.png"), screen);
}

function detectFixture(image) {
    for (let scale = 1; scale <= 8; scale++) {
        const originX = 0;
        const originY = 0;
        if (!matchesScaledPattern(image, originX + GUI_TOP_LEFT_MARKER_X * scale, originY + GUI_TOP_LEFT_MARKER_Y * scale, scale, GUI_TOP_LEFT_MARKER))
            continue;
        if (!matchesScaledPattern(image, originX + GUI_BOTTOM_RIGHT_MARKER_X * scale, originY + GUI_BOTTOM_RIGHT_MARKER_Y * scale, scale, GUI_BOTTOM_RIGHT_MARKER))
            continue;

        const slots = [];
        for (let index = 0; index < 27; index++) {
            const column = index % 9;
            const row = Math.floor(index / 9);
            const markerX = originX + (8 + column * 18 + ITEM_MARKER_X) * scale;
            const markerY = originY + (84 + row * 18 + ITEM_MARKER_Y) * scale;
            if ([...directItemTextures, ...blockItems].some(item =>
                matchesScaledPattern(image, markerX, markerY, scale, buildItemMarker(item.markerCode))))
                slots.push(index + 1);
        }
        return { scale, slots };
    }
    return { scale: 0, slots: [] };
}

function matchesScaledPattern(image, startX, startY, scale, pattern) {
    for (let y = 0; y < pattern.length; y++) {
        for (let x = 0; x < pattern[y].length; x++) {
            const sampleX = startX + x * scale + Math.floor(scale / 2);
            const sampleY = startY + y * scale + Math.floor(scale / 2);
            if (sampleX < 0 || sampleY < 0 || sampleX >= image.width || sampleY >= image.height)
                return false;
            const actual = getPixel(image, sampleX, sampleY);
            const expected = pattern[y][x];
            for (let channel = 0; channel < 3; channel++) {
                if (Math.abs(actual[channel] - expected[channel]) > 8)
                    return false;
            }
        }
    }
    return true;
}

function createImage(width, height, color) {
    const data = Buffer.alloc(width * height * 4);
    const image = { width, height, data };
    for (let y = 0; y < height; y++)
        for (let x = 0; x < width; x++)
            setPixel(image, x, y, color);
    return image;
}

function blit(target, source, targetX, targetY, maxWidth = source.width, maxHeight = source.height) {
    const width = Math.min(maxWidth, source.width);
    const height = Math.min(maxHeight, source.height);
    for (let y = 0; y < height; y++) {
        for (let x = 0; x < width; x++) {
            if (targetX + x < 0 || targetY + y < 0 || targetX + x >= target.width || targetY + y >= target.height)
                continue;
            const sourceColor = getPixel(source, x, y);
            if (sourceColor[3] === 0)
                continue;
            setPixel(target, targetX + x, targetY + y, sourceColor);
        }
    }
}

function scaleNearest(source, scale) {
    const target = createImage(source.width * scale, source.height * scale, [0, 0, 0, 0]);
    for (let y = 0; y < target.height; y++) {
        for (let x = 0; x < target.width; x++)
            setPixel(target, x, y, getPixel(source, Math.floor(x / scale), Math.floor(y / scale)));
    }
    return target;
}

function getPixel(image, x, y) {
    const offset = (y * image.width + x) * 4;
    return [image.data[offset], image.data[offset + 1], image.data[offset + 2], image.data[offset + 3]];
}

function setPixel(image, x, y, color) {
    const offset = (y * image.width + x) * 4;
    image.data[offset] = color[0];
    image.data[offset + 1] = color[1];
    image.data[offset + 2] = color[2];
    image.data[offset + 3] = color[3];
}

function readPng(filePath) {
    const file = fs.readFileSync(filePath);
    const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
    if (!file.subarray(0, 8).equals(signature))
        throw new Error(`Niepoprawny PNG: ${filePath}`);

    let offset = 8;
    let header = null;
    let palette = null;
    let transparency = null;
    const compressedParts = [];

    while (offset < file.length) {
        const length = file.readUInt32BE(offset);
        const type = file.toString("ascii", offset + 4, offset + 8);
        const data = file.subarray(offset + 8, offset + 8 + length);
        offset += 12 + length;

        if (type === "IHDR") {
            header = {
                width: data.readUInt32BE(0),
                height: data.readUInt32BE(4),
                bitDepth: data[8],
                colorType: data[9],
                interlace: data[12]
            };
        } else if (type === "PLTE") {
            palette = Buffer.from(data);
        } else if (type === "tRNS") {
            transparency = Buffer.from(data);
        } else if (type === "IDAT") {
            compressedParts.push(Buffer.from(data));
        } else if (type === "IEND") {
            break;
        }
    }

    if (!header || header.bitDepth !== 8 || header.interlace !== 0)
        throw new Error(`Nieobslugiwany format PNG: ${filePath}`);

    const channelsByColorType = { 0: 1, 2: 3, 3: 1, 4: 2, 6: 4 };
    const channels = channelsByColorType[header.colorType];
    if (!channels)
        throw new Error(`Nieobslugiwany typ koloru PNG ${header.colorType}: ${filePath}`);

    const packed = zlib.inflateSync(Buffer.concat(compressedParts));
    const stride = header.width * channels;
    const raw = Buffer.alloc(stride * header.height);
    let packedOffset = 0;

    for (let y = 0; y < header.height; y++) {
        const filter = packed[packedOffset++];
        const rowOffset = y * stride;
        for (let x = 0; x < stride; x++) {
            const encoded = packed[packedOffset++];
            const left = x >= channels ? raw[rowOffset + x - channels] : 0;
            const up = y > 0 ? raw[rowOffset + x - stride] : 0;
            const upLeft = y > 0 && x >= channels ? raw[rowOffset + x - stride - channels] : 0;
            let value;
            if (filter === 0) value = encoded;
            else if (filter === 1) value = (encoded + left) & 0xff;
            else if (filter === 2) value = (encoded + up) & 0xff;
            else if (filter === 3) value = (encoded + Math.floor((left + up) / 2)) & 0xff;
            else if (filter === 4) value = (encoded + paeth(left, up, upLeft)) & 0xff;
            else throw new Error(`Nieobslugiwany filtr PNG ${filter}: ${filePath}`);
            raw[rowOffset + x] = value;
        }
    }

    const rgba = Buffer.alloc(header.width * header.height * 4);
    for (let pixel = 0; pixel < header.width * header.height; pixel++) {
        const source = pixel * channels;
        const target = pixel * 4;
        if (header.colorType === 0) {
            rgba[target] = rgba[target + 1] = rgba[target + 2] = raw[source];
            rgba[target + 3] = 255;
        } else if (header.colorType === 2) {
            rgba[target] = raw[source];
            rgba[target + 1] = raw[source + 1];
            rgba[target + 2] = raw[source + 2];
            rgba[target + 3] = 255;
        } else if (header.colorType === 3) {
            const index = raw[source];
            if (!palette || index * 3 + 2 >= palette.length)
                throw new Error(`Uszkodzona paleta PNG: ${filePath}`);
            rgba[target] = palette[index * 3];
            rgba[target + 1] = palette[index * 3 + 1];
            rgba[target + 2] = palette[index * 3 + 2];
            rgba[target + 3] = transparency && index < transparency.length ? transparency[index] : 255;
        } else if (header.colorType === 4) {
            rgba[target] = rgba[target + 1] = rgba[target + 2] = raw[source];
            rgba[target + 3] = raw[source + 1];
        } else {
            rgba[target] = raw[source];
            rgba[target + 1] = raw[source + 1];
            rgba[target + 2] = raw[source + 2];
            rgba[target + 3] = raw[source + 3];
        }
    }

    return { width: header.width, height: header.height, data: rgba };
}

function writePng(filePath, image) {
    const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
    const header = Buffer.alloc(13);
    header.writeUInt32BE(image.width, 0);
    header.writeUInt32BE(image.height, 4);
    header[8] = 8;
    header[9] = 6;
    header[10] = 0;
    header[11] = 0;
    header[12] = 0;

    const raw = Buffer.alloc((image.width * 4 + 1) * image.height);
    for (let y = 0; y < image.height; y++) {
        const rowOffset = y * (image.width * 4 + 1);
        raw[rowOffset] = 0;
        image.data.copy(raw, rowOffset + 1, y * image.width * 4, (y + 1) * image.width * 4);
    }

    const output = Buffer.concat([
        signature,
        pngChunk("IHDR", header),
        pngChunk("IDAT", zlib.deflateSync(raw, { level: 9 })),
        pngChunk("IEND", Buffer.alloc(0))
    ]);
    fs.mkdirSync(path.dirname(filePath), { recursive: true });
    fs.writeFileSync(filePath, output);
}

function pngChunk(type, data) {
    const typeBuffer = Buffer.from(type, "ascii");
    const result = Buffer.alloc(12 + data.length);
    result.writeUInt32BE(data.length, 0);
    typeBuffer.copy(result, 4);
    data.copy(result, 8);
    result.writeUInt32BE(crc32(Buffer.concat([typeBuffer, data])), 8 + data.length);
    return result;
}

function crc32(buffer) {
    let crc = 0xffffffff;
    for (const byte of buffer) {
        crc ^= byte;
        for (let bit = 0; bit < 8; bit++)
            crc = (crc >>> 1) ^ ((crc & 1) ? 0xedb88320 : 0);
    }
    return (crc ^ 0xffffffff) >>> 0;
}

function paeth(a, b, c) {
    const p = a + b - c;
    const pa = Math.abs(p - a);
    const pb = Math.abs(p - b);
    const pc = Math.abs(p - c);
    if (pa <= pb && pa <= pc) return a;
    if (pb <= pc) return b;
    return c;
}

main();
