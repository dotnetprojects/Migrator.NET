using System.Linq.Expressions;

namespace Migrator.Framework
{
    public class ViewJoin : IViewElement
    {
        public string TableName { get; }
        public string TableAlias { get; }
        public string ColumnName { get; }
        public string ParentTableName { get; }
        public string ParentTableAlias { get; }
        public string ParentColumnName { get; }
        public JoinType JoinType { get; }

        public ViewJoin(string tableName, string columnName, string parentTableName, string parentColumnName, JoinType joinType)
            : this(tableName, string.Empty, columnName, parentTableName, string.Empty, parentColumnName, joinType)
            => Expression.Empty();

        public ViewJoin(string tableName, string tableAlias, string columnName, string parentTableName, string parentColumnName, JoinType joinType)
            : this(tableName, tableAlias, columnName, parentTableName, string.Empty, parentColumnName, joinType)
            => Expression.Empty();

        public ViewJoin(JoinType joinType, string tableName, string columnName, string parentTableName, string parentTableAlias, string parentColumnName)
            : this(tableName, string.Empty, columnName, parentTableName, parentTableAlias, parentColumnName, joinType)
            => Expression.Empty();

        public ViewJoin(string tableName, string tableAlias, string columnName, string parentTableName, string parentTableAlias, string parentColumnName, JoinType joinType)
        {
            TableName = tableName;
            TableAlias = tableAlias;
            ColumnName = columnName;
            ParentTableName = parentTableName;
            ParentTableAlias = parentTableAlias;
            ParentColumnName = parentColumnName;
            JoinType = joinType;
        }
    }
}
