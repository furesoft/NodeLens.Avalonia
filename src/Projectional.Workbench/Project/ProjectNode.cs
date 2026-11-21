namespace Projectional.Workbench;

/// <summary>What a row of the project explorer is, which decides its icon and whether it can be opened.</summary>
internal enum ProjectNode
{
    /// <summary>A heading the rows below belong to, such as <c>Documents</c>.</summary>
    Group,

    /// <summary>A language project opened from a folder.</summary>
    Project,

    /// <summary>One of a project's aspect folders, such as <c>structure</c>.</summary>
    Folder,

    /// <summary>A definition document of a project, open or not.</summary>
    File,

}
