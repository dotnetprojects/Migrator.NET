using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using DotNetProjects.Migrator.Framework;

namespace DotNetProjects.Migrator.Providers;

/// <summary>
/// This class maps a DbType to names.
/// </summary>
/// <remarks>
/// Associations may be marked with a capacity. Calling the <c>Get()</c>
/// method with a type and actual size n will return the associated
/// name with smallest capacity >= n, if available and an unmarked
/// default type otherwise.
/// Eg, setting
/// <code>
///		Names.Put(DbType,			"TEXT" );
///		Names.Put(DbType,	255,	"VARCHAR($l)" );
///		Names.Put(DbType,	65534,	"LONGVARCHAR($l)" );
/// </code>
/// will give you back the following:
/// <code>
///		Names.Get(DbType)			// --> "TEXT" (default)
///		Names.Get(DbType,100)		// --> "VARCHAR(100)" (100 is in [0:255])
///		Names.Get(DbType,1000)	// --> "LONGVARCHAR(1000)" (100 is in [256:65534])
///		Names.Get(DbType,100000)	// --> "TEXT" (default)
/// </code>
/// On the other hand, simply putting
/// <code>
///		Names.Put(DbType, "VARCHAR($l)" );
/// </code>
/// would result in
/// <code>
///		Names.Get(DbType)			// --> "VARCHAR($l)" (will cause trouble)
///		Names.Get(DbType,100)		// --> "VARCHAR(100)" 
///		Names.Get(DbType,1000)	// --> "VARCHAR(1000)"
///		Names.Get(DbType,10000)	// --> "VARCHAR(10000)"
/// </code>
/// </remarks>
public class TypeNames
{
    public const string LengthPlaceHolder = "$l";
    public const string PrecisionPlaceHolder = "$p";
    public const string ScalePlaceHolder = "$s";

    private readonly Dictionary<MigratorDbType, string> defaults = new Dictionary<MigratorDbType, string>();

    private readonly Dictionary<MigratorDbType, string> parametrized = new Dictionary<MigratorDbType, string>();

    private readonly Dictionary<string, MigratorDbType> aliases = new Dictionary<string, MigratorDbType>();

    private readonly Dictionary<MigratorDbType, SortedList<int, string>> weighted =
        new Dictionary<MigratorDbType, SortedList<int, string>>();

    public DbType GetDbType(string type)
    {
        type = type.Trim().ToLower();
        var retval = defaults.Where(x => x.Value.Trim().ToLower().StartsWith(type)).Select(x => x.Key);
        if (retval.Any())
        {
            return (DbType)retval.First();
        }

        retval = weighted.Where(x => x.Value.Where(y => y.Value.Trim().ToLower().StartsWith(type)).Any()).Select(x => x.Key);
        if (retval.Any())
        {
            return (DbType)retval.First();
        }

        var alias = aliases.Where(x => x.Key.Trim().ToLower().StartsWith(type));

        if (alias.Any())
        {
            return (DbType)alias.First().Value;
        }

        return DbType.AnsiString;
    }

    /// <summary>
    /// Get default type name for specified type
    /// </summary>
    /// <param name="typecode">the type key</param>
    /// <returns>the default type name associated with the specified key</returns>
    public string Get(DbType typecode)
    {
        string result;
        if (!defaults.TryGetValue((MigratorDbType)typecode, out result))
        {
            throw new ArgumentException("Dialect does not support DbType." + typecode, "typecode");
        }
        return result;
    }

    /// <summary>
    /// Get default type name for specified type
    /// </summary>
    /// <param name="typecode">the type key</param>
    /// <returns>the default type name associated with the specified key</returns>
    public string GetParametrized(DbType typecode)
    {
        string result;
        if (!parametrized.TryGetValue((MigratorDbType)typecode, out result))
        {
            return null;
        }
        return result;
    }

    /// <summary>
    /// Get the type name specified type and size
    /// </summary>
    /// <param name="typecode">the type key</param>
    /// <param name="size">the SQL length </param>
    /// <param name="scale">the SQL scale </param>
    /// <param name="precision">the SQL precision </param>
    /// <returns>
    /// The associated name with smallest capacity >= size if available and the
    /// default type name otherwise
    /// </returns>
    public string Get(DbType typecode, int size, int precision, int scale)
    {
        SortedList<int, string> map;
        weighted.TryGetValue((MigratorDbType)typecode, out map);
        if (map != null && map.Count > 0)
        {
            foreach (var entry in map)
            {
                if (size <= entry.Key)
                {
                    return Replace(entry.Value, size, precision, scale);
                }
            }
        }
        //Could not find a specific type for the size, using the default
        return Get(typecode);
    }

    private static string Replace(string type, int size, int precision, int scale)
    {
        type = StringUtils.ReplaceOnce(type, LengthPlaceHolder, size.ToString());
        type = StringUtils.ReplaceOnce(type, ScalePlaceHolder, scale.ToString());
        return StringUtils.ReplaceOnce(type, PrecisionPlaceHolder, precision.ToString());
    }

    /// <summary>
    /// Set a type name for specified type key and capacity
    /// </summary>
    /// <param name="typecode">the type key</param>
    /// <param name="capacity">the (maximum) type size/length</param>
    /// <param name="value">The associated name</param>
    public void Put(DbType typecode, int capacity, string value)
    {
        SortedList<int, string> map;
        if (!weighted.TryGetValue((MigratorDbType)typecode, out map))
        {
            // add new ordered map
            weighted[(MigratorDbType)typecode] = map = new SortedList<int, string>();
        }
        map[capacity] = value;
    }

    /// <summary>
    /// Set a type name for specified type key and capacity
    /// </summary>
    /// <param name="typecode">the type key</param>
    /// <param name="capacity">the (maximum) type size/length</param>
    /// <param name="value">The associated name</param>
    public void Put(MigratorDbType typecode, int capacity, string value)
    {
        SortedList<int, string> map;
        if (!weighted.TryGetValue(typecode, out map))
        {
            // add new ordered map
            weighted[typecode] = map = new SortedList<int, string>();
        }
        map[capacity] = value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="typecode"></param>
    /// <param name="value"></param>
    public void Put(DbType typecode, string value)
    {
        defaults[(MigratorDbType)typecode] = value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="typecode"></param>
    /// <param name="value"></param>
    public void Put(MigratorDbType typecode, string value)
    {
        defaults[typecode] = value;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="typecode"></param>
    /// <param name="value"></param>
    public void PutParametrized(DbType typecode, string value)
    {
        parametrized[(MigratorDbType)typecode] = value;
    }


    public void PutAlias(DbType typecode, string value)
    {
        aliases[value] = (MigratorDbType)typecode;
    }
}
