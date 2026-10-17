using System;
using System.Collections.Generic;
using System.Linq;

public interface ICharacterRuntimeProfileFactory
{
    CharacterRuntimeProfile Create(CharacterSpawnRequest request);
}
