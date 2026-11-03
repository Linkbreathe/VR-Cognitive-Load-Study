# Study environment asset sources

Retrieved 2026-11-03 for `warm-study-v1`. Selected upstream assets are CC0-1.0. Download URLs and SHA-256 hashes are recorded in `Evidence/LabEnvironment/asset-downloads.json`. Model and texture files were downloaded as data; no vendor scripts or packages were installed.

| Source | Creator | Used files | Integration |
|---|---|---|---|
| [White Plaster 02](https://polyhaven.com/a/white_plaster_02) | Rob Tuytel / Poly Haven | 1K normal map | Low-strength painted-wall micro-surface; diffuse excluded for clean, low-contrast walls |
| [Wood Floor](https://polyhaven.com/a/wood_floor) | Dimitrios Savva / Poly Haven | 1K diffuse and OpenGL normal | Warm matte floor, tiled at room scale |
| [Oak Veneer 01](https://polyhaven.com/a/oak_veneer_01) | Jenelle van Heerden / Poly Haven | 1K diffuse and OpenGL normal | Desks, shelves, door and restrained wood trim |
| [Furniture Kit](https://kenney.nl/assets/furniture-kit) | Kenney | `desk`, `chairDesk`, `computerScreen`, `computerKeyboard`, `computerMouse`, `lampRoundFloor`, `lampRoundTable`, `loungeChairRelax`, `pottedPlant`, `plantSmall1` FBX | Metric scaling, controlled material remapping; static props, inactive monitor, no animation/collision |

Poly Haven's [asset license](https://polyhaven.com/license) states CC0. The Kenney upstream `License.txt` is retained with the selected models. CC0 permits use, modification and redistribution; credits are retained for research provenance.

Earlier candidates retained for audit: [Metal Office Desk](https://polyhaven.com/a/metal_office_desk) by Ulan Cabanilla, [Terrazzo Tiles](https://polyhaven.com/a/terrazzo_tiles) by Amal Kumar and Concrete Floor 02. They were excluded after the user's warm-study correction. Unused candidates are outside runtime Resources and are not referenced by the scene.

Source textures are 1K with Android ASTC 6x6 and mipmaps. Material tinting and normal strength are integration settings; source image bytes are unchanged. No publisher previews or logos are embedded. Room architecture, books, shelves, cabinet details, rug and beveled unit mesh are authored in the project. There is no new text on book covers or decorative displays.
