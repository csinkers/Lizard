using Lizard.Util;

namespace Lizard.Core.Unwind;

public record UnwinderContext(CommandContext CommandContext, OffsetMemory StackMemory);
