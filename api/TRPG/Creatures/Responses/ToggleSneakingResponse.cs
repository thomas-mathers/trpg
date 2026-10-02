using System;
using System.Collections.Generic;
using System.Text;

namespace TRPG.Creatures.Responses;

public record ToggleSneakingResponse(Guid CreatureId, bool IsSneaking, float MovementSpeed);
