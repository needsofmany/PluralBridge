# Graph expansion deployment checklist

Stop traffic before the first script. These scripts have not been executed. Nothing in this checklist has been verified against the database.

The measured first call was slower and compilation events were reported with it. The exact waits in that delay were not identified.

The calling login needs EXECUTE on `dbo.pb_show_tree_step5_graph_expansion` and REFERENCES on `dbo.pb_WalkTopologyRows` and `dbo.pb_GraphEdgeRows`. REFERENCES on schema `dbo`, or ownership of the database, also covers the types. These scripts do not grant those permissions.

## Deploy

1. `D:\Src\Needs of the Many\PluralBridge\scripts\sqlserver\Members\013_graph_edge_table_types.sql`
2. `D:\Src\Needs of the Many\PluralBridge\scripts\sqlserver\Members\014_graph_expansion_result_set.sql`
3. `D:\Src\Needs of the Many\PluralBridge\scripts\sqlserver\Members\015_profile_graph_expansion_capture.sql`
4. `D:\Src\Needs of the Many\PluralBridge\scripts\sqlserver\Members\016_step4_graph_expansion_capture.sql`
5. Start the new application build.

In Visual Studio, stop PluralBridge before starting it again. Use the Stop button on the toolbar, or Shift+F5. That unloads the running API, which still has the old expansion call. After the four scripts have finished, press F5 to build and start the API again.

`009_inherit_example_visibility_permissions.sql` and `get_members_for_anchor.sql` are updated for their next manual run. Do not run them until step 2 has been deployed.

Do not run `D:\Src\Needs of the Many\PluralBridge\scripts\sqlserver\Left Panel Tree\pb_show_tree_step5_graph_expansion.sql`. That older file restores the temporary-table procedure.

## Recover

Stop traffic first. Restore the previous application only after steps 1 through 3.

1. `D:\Src\Needs of the Many\PluralBridge\scripts\sqlserver\Members\016_step4_graph_expansion_capture_recovery.sql`
2. `D:\Src\Needs of the Many\PluralBridge\scripts\sqlserver\Members\015_profile_graph_expansion_capture_recovery.sql`
3. `D:\Src\Needs of the Many\PluralBridge\scripts\sqlserver\Members\014_graph_expansion_result_set_recovery.sql`
4. Stop the API in Visual Studio with the Stop button or Shift+F5, then press F5 on the previous application build.
5. `D:\Src\Needs of the Many\PluralBridge\scripts\sqlserver\Members\013_graph_edge_table_types_recovery.sql`

## Compare

Run the before script before step 2. Run the after script once in each of two query windows after step 4. Then run the cache script in either window. Do not clear the cache.

- `D:\Src\Needs of the Many\PluralBridge\scripts\sqlserver\Diagnostics\Workspace\05_graph_expansion_edges_before.sql`
- `D:\Src\Needs of the Many\PluralBridge\scripts\sqlserver\Diagnostics\Workspace\06_graph_expansion_edges_after.sql`
- `D:\Src\Needs of the Many\PluralBridge\scripts\sqlserver\Diagnostics\Workspace\07_graph_expansion_cross_session_cache.sql`
