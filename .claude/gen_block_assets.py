import os, hashlib

BLOCK_DEF_SCRIPT = "71c622655d272264aa92cf2be9b30f2d"
BLOCK_STATE_SCRIPT = "2769f4293c46a2c4381ecbae6a6847fb"
BLOCK_MODEL_SCRIPT = "39b7875dc39711f4fab4ad63ddec4494"
LOOT_TABLE_SCRIPT = "f3021b5e12934554a906a373e48fc994"

BASE = "C:/Users/Clayton/Lithforge/Assets/Resources/Content"

def make_guid(name):
    return hashlib.md5(("lithforge-ocean-rivers-" + name).encode()).hexdigest()

blocks = [
    {"name": "clay", "hardness": 0.6, "blastResistance": 0.6, "materialType": 3,
     "soundGroup": "gravel", "collisionShape": 0, "renderLayer": 0, "isFluid": 0,
     "defaultTintType": -1, "lightEmission": 0, "lightFilter": 15, "mapColor": "#9FA4B0",
     "tags": ["lithforge:mineable_shovel"]},
    {"name": "mud", "hardness": 0.5, "blastResistance": 0.5, "materialType": 3,
     "soundGroup": "gravel", "collisionShape": 0, "renderLayer": 0, "isFluid": 0,
     "defaultTintType": -1, "lightEmission": 0, "lightFilter": 15, "mapColor": "#6B543F",
     "tags": ["lithforge:mineable_shovel"]},
    {"name": "ice", "hardness": 0.5, "blastResistance": 0.5, "materialType": 0,
     "soundGroup": "glass", "collisionShape": 0, "renderLayer": 2, "isFluid": 0,
     "defaultTintType": -1, "lightEmission": 0, "lightFilter": 2, "mapColor": "#A3C8ED",
     "tags": []},
]

def write_meta(path, guid):
    with open(path, "w", newline="\n") as f:
        f.write("fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

def write_texture_meta(path, guid):
    content = "fileFormatVersion: 2\nguid: " + guid + "\nTextureImporter:\n  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 13\n  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 1\n    sRGBTexture: 1\n    linearTexture: 0\n    fadeOut: 0\n    borderMipMap: 0\n    mipMapsPreserveCoverage: 0\n    alphaTestReferenceValue: 0.5\n    mipMapFadeDistanceStart: 1\n    mipMapFadeDistanceEnd: 3\n  bumpmap:\n    convertToNormalMap: 0\n    externalNormalMap: 0\n    heightScale: 0.25\n    normalMapFilter: 0\n    flipGreenChannel: 0\n  isReadable: 0\n  streamingMipmaps: 0\n  streamingMipmapsPriority: 0\n  vTOnly: 0\n  ignoreMipmapLimit: 0\n  grayScaleToAlpha: 0\n  generateCubemap: 6\n  cubemapConvolution: 0\n  seamlessCubemap: 0\n  textureFormat: 1\n  maxTextureSize: 2048\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 0\n    aniso: 1\n    mipBias: 0\n    wrapU: 0\n    wrapV: 0\n    wrapW: 0\n  nPOTScale: 1\n  lightmap: 0\n  compressionQuality: 50\n  spriteMode: 0\n  spriteExtrude: 1\n  spriteMeshType: 1\n  alignment: 0\n  spritePivot: {x: 0.5, y: 0.5}\n  spritePixelsToUnits: 100\n  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n  spriteGenerateFallbackPhysicsShape: 1\n  alphaUsage: 1\n  alphaIsTransparency: 0\n  spriteTessellationDetail: -1\n  textureType: 0\n  textureShape: 1\n  singleChannelComponent: 0\n  flipbookRows: 1\n  flipbookColumns: 1\n  maxTextureSizeSet: 0\n  compressionQualitySet: 0\n  textureFormatSet: 0\n  ignorePngGamma: 0\n  applyGammaDecoding: 0\n  swizzle: 50462976\n  cookieLightType: 0\n  platformSettings:\n  - serializedVersion: 4\n    buildTarget: DefaultTexturePlatform\n    maxTextureSize: 2048\n    resizeAlgorithm: 0\n    textureFormat: -1\n    textureCompression: 1\n    compressionQuality: 50\n    crunchedCompression: 0\n    allowsAlphaSplitting: 0\n    overridden: 0\n    ignorePlatformSupport: 0\n    androidETC2FallbackOverride: 0\n    forceMaximumCompressionQuality_BC6H_BC7: 0\n  spriteSheet:\n    serializedVersion: 2\n    sprites: []\n    outline: []\n    customData: \n    physicsShape: []\n    bones: []\n    spriteID: \n    internalID: 0\n    vertices: []\n    indices: \n    edges: []\n    weights: []\n    secondaryTextures: []\n    spriteCustomMetadata:\n      entries: []\n    nameFileIdTable: {}\n  mipmapLimitGroupName: \n  pSDRemoveMatte: 0\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    with open(path, "w", newline="\n") as f:
        f.write(content)

for block in blocks:
    name = block["name"]
    tex_guid = make_guid("tex-" + name)
    model_guid = make_guid("model-" + name)
    mapping_guid = make_guid("mapping-" + name)
    definition_guid = make_guid("definition-" + name)
    loot_guid = make_guid("loot-" + name)

    # 1. Texture meta
    write_texture_meta(BASE + "/Textures/Blocks/" + name + ".png.meta", tex_guid)

    # 2. BlockModel
    model_yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: " + BLOCK_MODEL_SCRIPT + ", type: 3}\n  m_Name: " + name + "\n  m_EditorClassIdentifier: Lithforge.Runtime::Lithforge.Runtime.Content.Models.BlockModel\n  parent: {fileID: 0}\n  builtInParent: 1\n  textures:\n  - variable: all\n    texture: {fileID: 2800000, guid: " + tex_guid + ", type: 3}\n    variableReference: \n  elements: []\n"
    with open(BASE + "/Models/" + name + ".asset", "w", newline="\n") as f:
        f.write(model_yaml)
    write_meta(BASE + "/Models/" + name + ".asset.meta", model_guid)

    # 3. BlockStateMapping
    mapping_yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: " + BLOCK_STATE_SCRIPT + ", type: 3}\n  m_Name: " + name + "\n  m_EditorClassIdentifier: Lithforge.Runtime::Lithforge.Runtime.Content.Blocks.BlockStateMapping\n  variants:\n  - variantKey: \n    model: {fileID: 11400000, guid: " + model_guid + ", type: 2}\n    rotationX: 0\n    rotationY: 0\n    uvlock: 0\n    weight: 1\n"
    with open(BASE + "/BlockStates/" + name + ".asset", "w", newline="\n") as f:
        f.write(mapping_yaml)
    write_meta(BASE + "/BlockStates/" + name + ".asset.meta", mapping_guid)

    # 4. LootTable
    loot_yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: " + LOOT_TABLE_SCRIPT + ", type: 3}\n  m_Name: " + name + "\n  m_EditorClassIdentifier: Lithforge.Runtime::Lithforge.Runtime.Content.Loot.LootTable\n  _namespace: lithforge\n  tableName: blocks/" + name + "\n  type: block\n  pools:\n  - rollsMin: 1\n    rollsMax: 1\n    entries:\n    - type: item\n      item: {fileID: 0}\n      itemName: lithforge:" + name + "\n      weight: 1\n      conditions: []\n      functions: []\n    conditions: []\n"
    with open(BASE + "/LootTables/" + name + ".asset", "w", newline="\n") as f:
        f.write(loot_yaml)
    write_meta(BASE + "/LootTables/" + name + ".asset.meta", loot_guid)

    # 5. BlockDefinition
    tags_str = "  tags: []" if not block["tags"] else "  tags:\n" + "\n".join("  - " + t for t in block["tags"])
    def_yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: " + BLOCK_DEF_SCRIPT + ", type: 3}\n  m_Name: " + name + "\n  m_EditorClassIdentifier: Lithforge.Runtime::Lithforge.Runtime.Content.Blocks.BlockDefinition\n  _namespace: lithforge\n  blockName: " + name + "\n  hardness: " + str(block["hardness"]) + "\n  blastResistance: " + str(block["blastResistance"]) + "\n  requiresTool: 0\n  materialType: " + str(block["materialType"]) + "\n  requiredToolLevel: 0\n  soundGroup: " + block["soundGroup"] + "\n  collisionShape: " + str(block["collisionShape"]) + "\n  renderLayer: " + str(block["renderLayer"]) + "\n  isFluid: " + str(block["isFluid"]) + "\n  defaultTintType: " + str(block["defaultTintType"]) + "\n  lightEmission: " + str(block["lightEmission"]) + "\n  lightFilter: " + str(block["lightFilter"]) + "\n  mapColor: '" + block["mapColor"] + "'\n  lootTable: {fileID: 11400000, guid: " + loot_guid + ", type: 2}\n  blockStateMapping: {fileID: 11400000, guid: " + mapping_guid + ", type: 2}\n  properties: []\n" + tags_str + "\n"
    with open(BASE + "/Blocks/" + name + ".asset", "w", newline="\n") as f:
        f.write(def_yaml)
    write_meta(BASE + "/Blocks/" + name + ".asset.meta", definition_guid)

    print("Created all assets for " + name)

print("Done!")