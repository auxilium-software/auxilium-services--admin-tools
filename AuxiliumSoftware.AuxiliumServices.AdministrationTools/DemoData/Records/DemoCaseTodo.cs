using AuxiliumSoftware.AuxiliumServices.Common.EntityFramework.Enumerators;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuxiliumSoftware.AuxiliumServices.AdministrationTools.DemoData.Records
{
    public sealed record DemoCaseTodo
    {
        public string? By { get; init; }
        public required string Summary { get; init; }
        public string? Description { get; init; }
        public TodoPriorityEnum Priority { get; init; } = TodoPriorityEnum.Medium;
        public TodoStatusEnum Status { get; init; } = TodoStatusEnum.NeedsAction;
        public string? AssignedTo { get; init; }
        public string? Due { get; init; }
        public string? CreatedAt { get; init; }
        public string? CompletionNote { get; init; }
    }
}
