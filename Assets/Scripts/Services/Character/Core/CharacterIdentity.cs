using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class CharacterIdentity : SerializedMonoBehaviour
{
    [SerializeField]
    [ReadOnly]
    private CharacterActor actor;
    [SerializeField]
    private CharacterSO data;
    [SerializeField]
    [ReadOnly]
    private CharacterRuntimeProfile profile;
    [SerializeField]
    [ReadOnly]
    private CharacterType characterType = CharacterType.Customer;
    [SerializeField]
    [ReadOnly]
    private CharacterRole role = CharacterRole.Regular;
    [SerializeField]
    [ReadOnly]
    private string persistentId = string.Empty;

    public CharacterSO Data => data;
    public CharacterRuntimeProfile Profile => profile;
    public CharacterType CharacterType => characterType;
    public CharacterRole Role => role;
    public string PersistentId => persistentId;
    public CharacterId TypedPersistentId => (CharacterId)persistentId;
    public int TemplateId => data != null ? data.id : -1;
    public bool IsOwner => role == CharacterRole.Owner;
    public bool CanLeaveByDissatisfaction => !IsOwner;
    public bool CanRebel => !IsOwner;
    public string DisplayName
    {
        get
        {
            string generatedName = actor != null
                && actor.Progression != null
                && actor.Progression.GrowthState.initialized
                    ? actor.Progression.GrowthState.displayName
                    : string.Empty;
            if (!string.IsNullOrWhiteSpace(generatedName))
            {
                return generatedName;
            }

            if (!string.IsNullOrWhiteSpace(data != null ? data.characterName : null))
            {
                return data.characterName;
            }

            if (!string.IsNullOrWhiteSpace(actor != null ? actor.name : null))
            {
                return actor.name;
            }

            return name;
        }
    }
    public string SpeciesTag => profile != null ? profile.SpeciesTag : data != null ? data.SpeciesTag : string.Empty;

    private void Awake()
    {
        Bind(GetComponent<CharacterActor>());
        if (data != null && profile == null)
        {
            SetData(data);
        }
    }

    public void Bind(CharacterActor owner)
    {
        actor = owner;
    }

    public void SetData(CharacterSO nextData)
    {
        data = nextData;
        profile = data != null ? data.CreateRuntimeProfile() : null;
        characterType = data != null ? data.characterType : CharacterType.Customer;
        role = data != null ? data.role : CharacterRole.Regular;
        if (role == CharacterRole.Owner)
        {
            persistentId = CharacterId.Owner.Value;
        }
    }

    public void SetPersistentId(string value)
    {
        SetPersistentId(IsOwner ? CharacterId.Owner : (CharacterId)value);
    }

    public void SetPersistentId(CharacterId value)
    {
        CharacterId resolved = IsOwner ? CharacterId.Owner : value;
        if (!resolved.IsValid)
        {
            throw new System.ArgumentException(
                "A valid CharacterId is required.",
                nameof(value));
        }

        persistentId = resolved.Value;
    }

    public void SetCharacterType(CharacterType nextType)
    {
        characterType = nextType;
    }

    public string GetSpeciesShortDescription()
    {
        return profile != null ? profile.GetShortDescription() : string.Empty;
    }
}
