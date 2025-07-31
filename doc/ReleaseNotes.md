# Release Notes

## 7.0.159 - 7.0.209 (Nuget Version)
### Breaking Changes
+ Minimum SQLite Version: 3.8.2 (2013-12-06)

#### ColumnProperty
+ Removed `ForeignKey` use method `AddForeignKey(...)` instead.
+ `Unique` is now obsolete because you cannot add a constraint name using it. Removing it by name is therefore impossible without investigation. 

### Other changes
Several fixes see PRs