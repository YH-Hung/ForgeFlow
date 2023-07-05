namespace ForgeFlow;

public class ForgeUserContext
{
    public string ResourceType { get; set; }

    public ForgeUserContext(string resourceType)
    {
        ResourceType = resourceType;
    }

    public string GetResourceType()
    {
        return ResourceType;
    }
}