# Current modular character

## Files to keep
- `Character.blend`: clean editable source, five current mesh modules and a review camera. No historical scenes or obsolete armatures. Bone weights on body modules are retained; the canonical runtime skeleton/bind poses live in the Unity prefab.
- `Export/`: geometry exchange files and palette. Body/clean head/hair come from the Blender exporter. Glasses and sunglasses are retained editable mesh data, with production assets in Unity.
- `../../Tools/Skins/export_character.py`: current Blender export script (path relative to project root: `Tools/Skins/export_character.py`).
- `Assets/Models/Skins/Skin01/`: runtime prefab, meshes, materials, accessory definitions and shop previews.

## Reimport and verify
Open `Character.blend` in Blender. Run the exporter with its actual `__file__` set, for example from Blender's Python console:

```python
p = 'D:/UnityProjects/ClimbUp/Tools/Skins/export_character.py'
exec(compile(open(p, encoding='utf-8-sig').read(), p, 'exec'), {'__file__': p, '__name__': '__main__'})
```

In Unity use `ClimbUp > Skins > Import Current Character`, then `Rebuild Current Catalog and Previews` as needed. Import updates assets in place and retains prefab GUID, skeleton, bind poses and saved choices. The existing game uses a renderer scale of 100; the importer handles that coordinate contract.

Editor checks: `Skin01Validation.Validate()`, `ValidateApprovedRig()`, and `WardrobeShopValidation.ValidatePurchases()`. Reports and screenshots go to ignored `Temp/SkinWork/Unity`.

The wardrobe has six persistent slots: hair/hats, head, glasses, torso, legs and feet. Empty slots restore the base module or remove overlays. The active catalog contains short hair, glasses and sunglasses. Base body: 4952 triangles; with hair: 5196; assembled outfits must stay under 6000. The clean-head asset is an alternative module for authoring/validation, not a duplicate shop card.

Historical reviews, backup models and old generation scripts remain local and are excluded by `.gitignore`. They are not dependencies of the game or current import workflow and should not be used to rebuild the production character.
