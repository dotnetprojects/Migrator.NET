namespace DotNetProjects.Migrator.Framework.Data.Models.Oracle;

public class VSession
{
    /// <summary>
    /// Gets or sets the "serial#"
    /// </summary>
    public string SerialHashTag { get; set; }

    /// <summary>
    /// Gets or sets the session id (SID).
    /// </summary>
    public string SID { get; set; }

    /// <summary>
    /// Gets or sets the user name.
    /// </summary>
    public string UserName { get; set; }
}