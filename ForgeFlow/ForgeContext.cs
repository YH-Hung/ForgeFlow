using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Forge.Attributes;
using Microsoft.Forge.DataContracts;
using Microsoft.Forge.TreeWalker;
using System.Text.Json;
namespace ForgeFlow;

public class ForgeContext
{
    //
// A simple ForgeTree schema.
// The tree walker will visit the Root node and execute the ForgeAction.
// In the child selector, a Roslyn snippet is used to get the persisted ActionResponse from the Action.
// If the Status is "Success," then tree walker will visit the TardigradeSuccess TreeNode.
// Otherwise, the TardigradeFail TreeNode will be visited.
// Both of these TreeNodes are Leaf-type nodes. These are used to clearly represent the tree walking session has completed successfully.
//
// More details here: https://github.com/microsoft/Forge/wiki/How-To:-Author-a-ForgeTree
//
    public const string TestForgeSchema = @"
    {
        ""Tree"":
        {
            ""Root"": {
                ""Type"": ""Action"",
                ""Actions"": {
                    ""Tardigrade_TardigradeAction"": {
                        ""Action"": ""TardigradeAction"",
                        ""Input"": {
                            ""Reason"": ""Testing Input""
                        }
                    }
                },
                ""ChildSelector"": [
                    {
                        ""ShouldSelect"": ""C#|Session.GetLastActionResponse().Status == \""Success\"" && UserContext.ResourceType == \""Container\"""",
                        ""Child"": ""TardigradeSuccess""
                    },
                    {
                        ""Child"": ""TardigradeFail""
                    }
                ]
            },
            ""TardigradeSuccess"": {
                ""Type"": ""Leaf""
            },
            ""TardigradeFail"": {
                ""Type"": ""Leaf""
            }
        }
    }";

    public void HandleRequest()
    {
        // Initialize a TreeWalkerSession, walk the provided ForgeTree schema starting at the "Root" node, and print the results.
        Console.WriteLine("OnBeforeWalkTree - TreeNodeKey: Root");

        TreeWalkerSession session = InitializeSession(jsonSchema: TestForgeSchema);
        string result = session.WalkTree("Root").GetAwaiter().GetResult();

        Console.WriteLine(
            $"OnAfterWalkTree - TreeNodeKey: {session.GetCurrentTreeNode().Result}, TreeWalkerStatus: {result}");
    }

    public TreeWalkerSession InitializeSession(string jsonSchema)
    {
        // Initialize required properties.
        Guid sessionId = Guid.NewGuid();
        ForgeTree forgeTree = JsonSerializer.Deserialize<ForgeTree>(jsonSchema);
        IForgeDictionary forgeState = new ForgeDictionary(new Dictionary<string, object>(), sessionId, sessionId);
        ITreeWalkerCallbacksV2 callbacks = new TreeWalkerCallbacks();
        CancellationToken token = new CancellationTokenSource().Token;

        // Initialize optional properties
        ForgeUserContext userContext = new ForgeUserContext("Container");
        Assembly forgeActionsAssembly = typeof(TardigradeAction).Assembly;
        ConcurrentDictionary<string, Script<object>> scriptCache = new ConcurrentDictionary<string, Script<object>>();

        TreeWalkerParameters parameters = new TreeWalkerParameters(
            sessionId,
            forgeTree,
            forgeState,
            callbacks,
            token)
        {
            UserContext = userContext,
            ForgeActionsAssembly = forgeActionsAssembly,
            ScriptCache = scriptCache
        };

        return new TreeWalkerSession(parameters);
    }

    //
// ForgeActions are easily created with the [ForgeAction] Attribute and inheriting from BaseAction.
// InputType can be optionally specified, allowing you to add Input to your ForgeAction on the schema.
//
// More details here: https://github.com/microsoft/Forge/wiki/How-To:-Author-a-ForgeAction
//
    [ForgeAction(InputType: typeof(TardigradeInput))]
    public class TardigradeAction : BaseAction
    {
        public override Task<ActionResponse> RunAction(ActionContext actionContext)
        {
            TardigradeInput input = (TardigradeInput)actionContext.ActionInput ?? new TardigradeInput();
            Console.WriteLine(
                $"OnExecuteAction - TreeNodeKey: {actionContext.TreeNodeKey}, ActionName: {actionContext.ActionName}, ActionInput: {input.Reason}");
            return Task.FromResult(new ActionResponse() { Status = "Success" });
        }
    }

    public class TardigradeInput
    {
        public string Reason { get; set; } = "DefaultValue";
    }

//
// Forge calls these callback methods while walking the tree.
// BeforeVisitNode is called before visiting each node, offering a convenient global hook into all TreeNodes.
// Similar for AfterVisitNode.
//
// More details here: https://github.com/microsoft/Forge/wiki/How-To:-Use-Forge-in-my-Application#ITreeWalkerCallbacks-Callbacks
//
    public class TreeWalkerCallbacks : ITreeWalkerCallbacksV2
    {
        public async Task BeforeVisitNode(TreeNodeContext treeNodeContext)
        {
            Console.WriteLine($"OnBeforeVisitNode - TreeNodeKey: {treeNodeContext.TreeNodeKey}");
        }

        public async Task AfterVisitNode(TreeNodeContext treeNodeContext)
        {
            Console.WriteLine($"OnAfterVisitNode - TreeNodeKey: {treeNodeContext.TreeNodeKey}");
        }
    }
}