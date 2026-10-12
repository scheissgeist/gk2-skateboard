"""Author the skateboard pixel-art sheet (art/skateboard.png) and a 10x preview.

Sheet layout: 4 cells of CELL_W x CELL_H, left to right:
  side_0, side_1 (board seen side-on, facing right; wheel frames)
  front_0, front_1 (board seen end-on, pointing toward camera)
Pivot for every cell is bottom-center; Plugin.cs mirrors these constants.
"""
from pathlib import Path
from PIL import Image

CELL_W, CELL_H = 26, 22

PAL = {
    ".": (0, 0, 0, 0),
    "o": (22, 17, 26, 255),     # outline
    "g": (46, 44, 56, 255),     # grip tape
    "G": (66, 63, 78, 255),     # grip highlight (near bevel)
    "e": (122, 58, 36, 255),    # deck edge wood
    "E": (166, 88, 52, 255),    # deck edge wood light
    "b": (226, 216, 190, 255),  # bone (skull decal / wheels)
    "B": (170, 158, 128, 255),  # bone shade
    "t": (126, 128, 142, 255),  # truck metal
}

SIDE_0 = [
    "...oooooooooooooooooooo...",
    "..oGGGGGGGGGGGGGGGGGGGGo..",
    ".oggggggggggbbbbggggggggo.",
    "ooggggggggggbobbggggggggoo",
    "oEgggggggggggbbgggggggggEo",
    ".oEEEEEEEEEEEEEEEEEEEEEEo.",
    "..oeeeeeeeeeeeeeeeeeeeeo..",
    "...oooottoooooooooottoo...",
    "....obbBo.........obbBo...",
    ".....oo............oo.....",
]
SIDE_1 = SIDE_0[:8] + [
    "....oBbbo.........oBbbo...",
    ".....oo............oo.....",
]

FRONT_0 = [
    "....oooooooo....",
    "...oGGGGGGGGo...",
    "..ooggggggggoo..",
    ".obotggggggtobo.",
    ".oBotggggggtoBo.",
    "..oogggbbgggoo..",
    "...ogggbbgggo...",
    "...oggbobbggo...",
    "...ogggbbgggo...",
    "...oggggggggo...",
    "...oggggggggo...",
    "...oggggggggo...",
    "..ooggggggggoo..",
    ".obotggggggtobo.",
    ".oBotggggggtoBo.",
    "..ooEEEEEEEEoo..",
    "...oeeeeeeeeo...",
    "....oooooooo....",
]
FRONT_1 = list(FRONT_0)
for r in (3, 13):
    FRONT_1[r] = ".oBotggggggtoBo."
for r in (4, 14):
    FRONT_1[r] = ".obotggggggtobo."


def paint(sheet, rows, cell):
    h, w = len(rows), len(rows[0])
    assert all(len(r) == w for r in rows), [len(r) for r in rows]
    x0 = cell * CELL_W + (CELL_W - w) // 2
    y0 = CELL_H - h  # bottom-aligned
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            sheet.putpixel((x0 + x, y0 + y), PAL[ch])


def main():
    root = Path(__file__).resolve().parent.parent
    sheet = Image.new("RGBA", (CELL_W * 4, CELL_H), (0, 0, 0, 0))
    for i, rows in enumerate((SIDE_0, SIDE_1, FRONT_0, FRONT_1)):
        paint(sheet, rows, i)
    (root / "art").mkdir(exist_ok=True)
    sheet.save(root / "art" / "skateboard.png")
    bg = Image.new("RGBA", sheet.size, (92, 110, 74, 255))
    bg.alpha_composite(sheet)
    bg.resize((sheet.width * 10, sheet.height * 10), Image.NEAREST).save(root / "art" / "skateboard_preview.png")


if __name__ == "__main__":
    main()
