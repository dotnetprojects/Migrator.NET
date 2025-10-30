namespace DotNetProjects.Migrator.Framework.Data.Models.Oracle;

/// <summary>
/// Represents the Oracle system table DBA_DATA_FILES.
/// </summary>
public class DBADataFiles
{
    /// <summary>
    /// Gets or sets the file name. (FILE_NAME)
    /// </summary>
    public string FileName { get; set; }

    /// <summary>
    /// Gets or sets the tablespace name. (TABLESPACE_NAME)
    /// </summary>
    public string TablespaceName { get; set; }
}
