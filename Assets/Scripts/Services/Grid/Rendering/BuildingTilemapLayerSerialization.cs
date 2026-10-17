using Sirenix.Serialization;

// Existing authored building and scene dictionaries keep their serialized names.
[assembly: BindTypeNameToType("GridTexture+TilemapLayer, Assembly-CSharp", typeof(BuildingTilemapLayer))]
