using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WildlifeHabitatDecorationPalette",
    menuName = "DungeonStory/Wildlife/Habitat Decoration Palette")]
public sealed class WildlifeHabitatDecorationPaletteSO : ScriptableObject
{
    public const string ResourcePath = "SO/Wildlife/WildlifeHabitatDecorationPalette";

    [Header("Consumable ground cover")]
    [SerializeField] private List<Sprite> flowerSprites = new List<Sprite>();

    [Header("Persistent exterior decoration")]
    [SerializeField] private List<Sprite> treeSprites = new List<Sprite>();
    [SerializeField] private List<Sprite> rockSprites = new List<Sprite>();

    [Header("Placement")]
    [SerializeField, Range(3, 8)] private int flowersPerGrassPatch = 5;
    [SerializeField, Range(2, 6)] private int flowersPerBrushPatch = 3;
    [SerializeField, Min(8)] private int scatteredTreeSpacing = 18;
    [SerializeField, Min(5)] private int scatteredRockSpacing = 9;

    public IReadOnlyList<Sprite> FlowerSprites => flowerSprites;
    public IReadOnlyList<Sprite> TreeSprites => treeSprites;
    public IReadOnlyList<Sprite> RockSprites => rockSprites;
    public int FlowersPerGrassPatch => Mathf.Clamp(flowersPerGrassPatch, 3, 8);
    public int FlowersPerBrushPatch => Mathf.Clamp(flowersPerBrushPatch, 2, 6);
    public int ScatteredTreeSpacing => Mathf.Max(8, scatteredTreeSpacing);
    public int ScatteredRockSpacing => Mathf.Max(5, scatteredRockSpacing);
    public bool IsComplete => flowerSprites.Any(sprite => sprite != null)
        && treeSprites.Any(sprite => sprite != null)
        && rockSprites.Any(sprite => sprite != null);

    public void Configure(
        IEnumerable<Sprite> flowers,
        IEnumerable<Sprite> trees,
        IEnumerable<Sprite> rocks)
    {
        flowerSprites = Sanitize(flowers);
        treeSprites = Sanitize(trees);
        rockSprites = Sanitize(rocks);
    }

    private static List<Sprite> Sanitize(IEnumerable<Sprite> source)
    {
        return (source ?? Enumerable.Empty<Sprite>())
            .Where(sprite => sprite != null)
            .Distinct()
            .ToList();
    }
}
