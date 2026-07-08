using System.CommandLine;

internal sealed class ToolsCommand : Command
{
    public ToolsCommand(
        ToolsAddCommand toolsAddCommand, 
        ToolsUpdateCommand toolsUpdateCommand, 
        ToolsRemoveCommand toolsRemoveCommand,
        ToolsListCommand toolsListCommand)
        : base("tools", "Manage tools")
    {
        Add(toolsAddCommand);
        Add(toolsUpdateCommand);
        Add(toolsRemoveCommand);
        Add(toolsListCommand);
    }
}