using UmbracoPrism.Shared.Models.Workflow;

namespace UmbracoPrism.MockBusinessApp.Services;

public static class ReferenceWorkflowQueues
{
    public const string WebUser = "web-user";
    public const string BusinessUser = "business-user";

    public static WorkflowAccessProfile WebUserProfile() => new()
    {
        VisibleQueues = [WebUser],
        StartableQueues = [WebUser],
        ActionableQueues = [WebUser]
    };

    public static WorkflowAccessProfile BusinessUserProfile() => new()
    {
        VisibleQueues = [BusinessUser],
        ActionableQueues = [BusinessUser],
        RestrictToInstanceOwner = false
    };
}
