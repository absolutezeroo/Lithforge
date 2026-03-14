import os, hashlib

BIOME_SCRIPT = "ee2bcbfe9d845f842a4bfdde0bc5c656"
BASE = "C:/Users/Clayton/Lithforge/Assets/Resources/Content/Biomes"

# Block definition GUIDs
GRAVEL  = "6dc247b419379ea4f985e2ab04f7ab37"
SAND    = "5a73cb824f577a94491f6cccba616bdd"
SANDSTONE = "73f24493020ca4545818b3222ea3e1b3"
STONE   = "9d87edab4555d3e48ac224061aeb0a6e"
DIRT    = "91c83ab8bf0318041b36b318c08acb6f"
GRASS   = "6939bb7628794ae4ea5b7f11fc81faa6"
MUD     = "4fabd49891c8be4bb815d58e5fbda5d8"
CLAY    = "76b315d8ecefa3ac21b9664ffc7ccbd9"

def make_guid(name):
    return hashlib.md5(("lithforge-biome-" + name).encode()).hexdigest()

def block_ref(guid):
    return "{fileID: 11400000, guid: " + guid + ", type: 2}"

def color_yaml(r, g, b, a=1.0):
    return "{r: " + str(r) + ", g: " + str(g) + ", b: " + str(b) + ", a: " + str(a) + "}"

def write_meta(path, guid):
    with open(path, "w", newline="\n") as f:
        f.write("fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

def write_biome(name, data, guid=None, create_meta=True):
    yaml = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: """ + BIOME_SCRIPT + """, type: 3}
  m_Name: """ + name + """
  m_EditorClassIdentifier: Lithforge.Runtime::Lithforge.Runtime.Content.WorldGen.BiomeDefinition
  _namespace: lithforge
  biomeName: """ + name + """
  temperatureMin: """ + str(data["tempMin"]) + """
  temperatureMax: """ + str(data["tempMax"]) + """
  temperatureCenter: """ + str(data["tempCenter"]) + """
  humidityMin: """ + str(data["humMin"]) + """
  humidityMax: """ + str(data["humMax"]) + """
  humidityCenter: """ + str(data["humCenter"]) + """
  topBlock: """ + block_ref(data["top"]) + """
  fillerBlock: """ + block_ref(data["filler"]) + """
  stoneBlock: """ + block_ref(data["stone"]) + """
  underwaterBlock: """ + block_ref(data["underwater"]) + """
  fillerDepth: """ + str(data["fillerDepth"]) + """
  treeDensity: """ + str(data["treeDensity"]) + """
  treeType: """ + str(data.get("treeType", 0)) + """
  continentalnessCenter: """ + str(data["cont"]) + """
  erosionCenter: """ + str(data["erosion"]) + """
  baseHeight: """ + str(data["baseHeight"]) + """
  heightAmplitude: """ + str(data["heightAmp"]) + """
  _weightSharpness: """ + str(data["sharpness"]) + """
  _isOcean: """ + str(1 if data.get("isOcean", False) else 0) + """
  _isFrozen: """ + str(1 if data.get("isFrozen", False) else 0) + """
  _isBeach: """ + str(1 if data.get("isBeach", False) else 0) + """
  waterColor: """ + color_yaml(*data["waterColor"]) + """
  mapColor: """ + color_yaml(*data["mapColor"]) + """
"""
    path = BASE + "/" + name + ".asset"
    with open(path, "w", newline="\n") as f:
        f.write(yaml)
    if create_meta and guid:
        write_meta(path + ".meta", guid)
    print("Created biome: " + name)


# ==========================================
# UPDATE EXISTING BIOMES (add new fields, keep existing meta)
# ==========================================

# Plains: inland, high erosion (flat), moderate temperature
write_biome("plains", {
    "tempMin": 0.3, "tempMax": 0.7, "tempCenter": 0.5,
    "humMin": 0.3, "humMax": 0.7, "humCenter": 0.5,
    "top": GRASS, "filler": DIRT, "stone": STONE, "underwater": DIRT,
    "fillerDepth": 3, "treeDensity": 0.02, "treeType": 0,
    "cont": 0.55, "erosion": 0.75,
    "baseHeight": 4, "heightAmp": 6,
    "sharpness": 7,
    "waterColor": (0.247, 0.463, 0.894),
    "mapColor": (0.55, 0.78, 0.35),
}, create_meta=False)

# Forest: inland, moderate erosion, moderate-high humidity
write_biome("forest", {
    "tempMin": 0.3, "tempMax": 0.65, "tempCenter": 0.45,
    "humMin": 0.55, "humMax": 1.0, "humCenter": 0.75,
    "top": GRASS, "filler": DIRT, "stone": STONE, "underwater": DIRT,
    "fillerDepth": 3, "treeDensity": 0.15, "treeType": 0,
    "cont": 0.6, "erosion": 0.55,
    "baseHeight": 6, "heightAmp": 8,
    "sharpness": 7,
    "waterColor": (0.247, 0.463, 0.894),
    "mapColor": (0.22, 0.55, 0.15),
}, create_meta=False)

# Desert: inland, very high erosion (flat), hot/dry
write_biome("desert", {
    "tempMin": 0.65, "tempMax": 1.0, "tempCenter": 0.85,
    "humMin": 0.0, "humMax": 0.3, "humCenter": 0.15,
    "top": SAND, "filler": SANDSTONE, "stone": STONE, "underwater": SAND,
    "fillerDepth": 4, "treeDensity": 0.0, "treeType": 0,
    "cont": 0.55, "erosion": 0.85,
    "baseHeight": 2, "heightAmp": 3,
    "sharpness": 8,
    "waterColor": (0.247, 0.463, 0.894),
    "mapColor": (0.85, 0.78, 0.5),
}, create_meta=False)

# Mountains: far inland, very low erosion (steep), cold
write_biome("mountains", {
    "tempMin": 0.0, "tempMax": 0.35, "tempCenter": 0.2,
    "humMin": 0.0, "humMax": 0.6, "humCenter": 0.35,
    "top": STONE, "filler": GRAVEL, "stone": STONE, "underwater": GRAVEL,
    "fillerDepth": 2, "treeDensity": 0.01, "treeType": 2,
    "cont": 0.82, "erosion": 0.15,
    "baseHeight": 22, "heightAmp": 28,
    "sharpness": 8,
    "waterColor": (0.247, 0.463, 0.894),
    "mapColor": (0.5, 0.5, 0.5),
}, create_meta=False)


# ==========================================
# CREATE NEW OCEAN BIOMES
# ==========================================

# Standard ocean: moderate temperature, gravel floor
write_biome("ocean", {
    "tempMin": 0.3, "tempMax": 0.65, "tempCenter": 0.5,
    "humMin": 0.0, "humMax": 1.0, "humCenter": 0.5,
    "top": GRAVEL, "filler": GRAVEL, "stone": STONE, "underwater": GRAVEL,
    "fillerDepth": 2, "treeDensity": 0.0,
    "cont": 0.15, "erosion": 0.5,
    "baseHeight": -17, "heightAmp": 5,
    "sharpness": 20, "isOcean": True,
    "waterColor": (0.247, 0.463, 0.894),
    "mapColor": (0.15, 0.15, 0.6),
}, guid=make_guid("ocean"))

# Deep ocean: lower floor, higher sharpness
write_biome("deep_ocean", {
    "tempMin": 0.3, "tempMax": 0.65, "tempCenter": 0.5,
    "humMin": 0.0, "humMax": 1.0, "humCenter": 0.5,
    "top": GRAVEL, "filler": GRAVEL, "stone": STONE, "underwater": GRAVEL,
    "fillerDepth": 2, "treeDensity": 0.0,
    "cont": 0.05, "erosion": 0.5,
    "baseHeight": -32, "heightAmp": 6,
    "sharpness": 25, "isOcean": True,
    "waterColor": (0.247, 0.463, 0.894),
    "mapColor": (0.1, 0.1, 0.45),
}, guid=make_guid("deep_ocean"))

# Warm ocean: hot temperature, sand floor, turquoise water
write_biome("warm_ocean", {
    "tempMin": 0.65, "tempMax": 1.0, "tempCenter": 0.8,
    "humMin": 0.0, "humMax": 1.0, "humCenter": 0.5,
    "top": SAND, "filler": SAND, "stone": STONE, "underwater": SAND,
    "fillerDepth": 2, "treeDensity": 0.0,
    "cont": 0.15, "erosion": 0.5,
    "baseHeight": -17, "heightAmp": 5,
    "sharpness": 20, "isOcean": True,
    "waterColor": (0.263, 0.835, 0.933),
    "mapColor": (0.2, 0.55, 0.7),
}, guid=make_guid("warm_ocean"))

# Cold ocean: cold temperature, gravel floor, darker water
write_biome("cold_ocean", {
    "tempMin": 0.1, "tempMax": 0.35, "tempCenter": 0.25,
    "humMin": 0.0, "humMax": 1.0, "humCenter": 0.5,
    "top": GRAVEL, "filler": GRAVEL, "stone": STONE, "underwater": GRAVEL,
    "fillerDepth": 2, "treeDensity": 0.0,
    "cont": 0.15, "erosion": 0.5,
    "baseHeight": -17, "heightAmp": 5,
    "sharpness": 20, "isOcean": True,
    "waterColor": (0.239, 0.341, 0.839),
    "mapColor": (0.12, 0.12, 0.55),
}, guid=make_guid("cold_ocean"))

# Frozen ocean: freezing temperature, ice surface, darkest water
write_biome("frozen_ocean", {
    "tempMin": 0.0, "tempMax": 0.15, "tempCenter": 0.08,
    "humMin": 0.0, "humMax": 1.0, "humCenter": 0.5,
    "top": GRAVEL, "filler": GRAVEL, "stone": STONE, "underwater": GRAVEL,
    "fillerDepth": 2, "treeDensity": 0.0,
    "cont": 0.15, "erosion": 0.5,
    "baseHeight": -17, "heightAmp": 5,
    "sharpness": 20, "isOcean": True, "isFrozen": True,
    "waterColor": (0.224, 0.220, 0.788),
    "mapColor": (0.55, 0.6, 0.75),
}, guid=make_guid("frozen_ocean"))


# ==========================================
# CREATE NEW TRANSITION BIOMES
# ==========================================

# Beach: coast zone, high erosion (gentle slope), warm-temperate
write_biome("beach", {
    "tempMin": 0.2, "tempMax": 1.0, "tempCenter": 0.6,
    "humMin": 0.0, "humMax": 1.0, "humCenter": 0.5,
    "top": SAND, "filler": SAND, "stone": SANDSTONE, "underwater": SAND,
    "fillerDepth": 5, "treeDensity": 0.0,
    "cont": 0.28, "erosion": 0.8,
    "baseHeight": 0, "heightAmp": 2,
    "sharpness": 10, "isBeach": True,
    "waterColor": (0.247, 0.463, 0.894),
    "mapColor": (0.9, 0.85, 0.6),
}, guid=make_guid("beach"))

# Stony shore (cliff): coast zone, low erosion (steep), dramatic drop
write_biome("stony_shore", {
    "tempMin": 0.0, "tempMax": 1.0, "tempCenter": 0.5,
    "humMin": 0.0, "humMax": 1.0, "humCenter": 0.5,
    "top": STONE, "filler": STONE, "stone": STONE, "underwater": GRAVEL,
    "fillerDepth": 1, "treeDensity": 0.0,
    "cont": 0.28, "erosion": 0.2,
    "baseHeight": 25, "heightAmp": 15,
    "sharpness": 15,
    "waterColor": (0.247, 0.463, 0.894),
    "mapColor": (0.55, 0.55, 0.55),
}, guid=make_guid("stony_shore"))

# Wetland: near coast, very high erosion (flat), warm-humid, mud surface
write_biome("wetland", {
    "tempMin": 0.45, "tempMax": 0.75, "tempCenter": 0.6,
    "humMin": 0.6, "humMax": 1.0, "humCenter": 0.8,
    "top": MUD, "filler": CLAY, "stone": STONE, "underwater": MUD,
    "fillerDepth": 3, "treeDensity": 0.0,
    "cont": 0.35, "erosion": 0.85,
    "baseHeight": -0.5, "heightAmp": 1.5,
    "sharpness": 4,
    "waterColor": (0.380, 0.482, 0.392),
    "mapColor": (0.35, 0.45, 0.3),
}, guid=make_guid("wetland"))

print("Done! Created/updated all biome assets.")