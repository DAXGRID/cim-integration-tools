using System.Data.Common;

namespace DAX.IO.Geometry
{
    public class SDEMetaData
    {
        private Dictionary<string, SDEObject> _sdeObjects = new Dictionary<string, SDEObject>();

        public List<SDEObject> SDEObjects() { return _sdeObjects.Values.ToList(); }

        /// <summary>
        /// Contruct SDEMetaData object holding table metadata from ArcSDE database
        /// </summary>
        /// <param name="sdeConnection"></param>
        /// <param name="sdeSchemaName"></param>
        public SDEMetaData(DbConnection sdeConnection, string sdeSchemaName, bool oracle = false)
        {
            string cmdSql = @"SELECT 
	                                tr.registration_id,
                                    tr.object_flags,
                                    gc.srid,
                                    tr.owner,
                                    tr.table_name,
                                    tr.rowid_column,
                                    gc.f_geometry_column,
                                    gc.g_table_name,
                                    sr.falsex,
                                    sr.falsey,
                                    sr.falsez,
                                    sr.xyunits,
                                    sr.zunits,
                                    sr.munits,
                                    gc.geometry_type,
                                    gc.storage_type,
                                    gc.coord_dimension
                                FROM 
                                  sde_schema.SDE_table_registry tr
                                LEFT OUTER JOIN 
                                  sde_schema.SDE_geometry_columns gc 
                                on 
                                  (gc.f_table_name = tr.table_name and gc.f_table_schema = tr.owner)
                                LEFT OUTER JOIN 
                                  sde_schema.SDE_spatial_references sr
                                on 
                                  (sr.srid = gc.srid)";

            cmdSql = cmdSql.Replace("sde_schema", sdeSchemaName);

            if (oracle)
                cmdSql = cmdSql.Replace("SDE_", "");

            var cmd = sdeConnection.CreateCommand();
            cmd.CommandText = cmdSql;

            // Execute the query
            var rdr = cmd.ExecuteReader();

            while (rdr.Read())
            {
                SDEObject sdeObject = new SDEObject();

                sdeObject.RegistrationID = rdr.GetInt32(0);
                sdeObject.objectFlags = rdr.GetInt32(1);
                sdeObject.srid = rdr.IsDBNull(2) ? -1 : rdr.GetInt32(2);
                sdeObject.Owner = rdr.GetString(3);
                sdeObject.TableName = rdr.GetString(4);
                sdeObject.IDColumnName = rdr.IsDBNull(5) ? null : rdr.GetString(5);
                sdeObject.GeometryColumnName = rdr.IsDBNull(6) ? null : rdr.GetString(6);
                sdeObject.FeatureTableName = rdr.IsDBNull(7) ? null : rdr.GetString(7);
                sdeObject.FalseX = rdr.IsDBNull(8) ? -1 : rdr.GetDouble(8);
                sdeObject.FalseY = rdr.IsDBNull(9) ? -1 : rdr.GetDouble(9);
                sdeObject.FalseZ = rdr.IsDBNull(10) ? -1 : rdr.GetDouble(10);
                sdeObject.XYUnits = rdr.IsDBNull(11) ? -1 : rdr.GetDouble(11);
                sdeObject.ZUnits = rdr.IsDBNull(12) ? -1 : rdr.GetDouble(12);
                sdeObject.MUnits = rdr.IsDBNull(13) ? -1 : rdr.GetDouble(13);
                sdeObject.GeometryType = rdr.IsDBNull(14) ? -1 : rdr.GetInt32(14);
                sdeObject.StorageType = rdr.IsDBNull(15) ? -1 : rdr.GetInt32(15);
                sdeObject.CoordDimension = rdr.IsDBNull(16) ? -1 : rdr.GetInt32(16);


                sdeObject.AddTableName = "a" + sdeObject.RegistrationID;
                sdeObject.DeleteTableName = "d" + sdeObject.RegistrationID;

                string key = (sdeObject.Owner + "." + sdeObject.TableName).ToLower();

                _sdeObjects.Add(key, sdeObject);
            }

            rdr.Close();

            // Henst felt information
            cmdSql = @"SELECT 
                          owner,
                          table_name,
                          column_name,
                          sde_type,
                          column_size
                        FROM 
                          sde_schema.SDE_column_registry";

            cmdSql = cmdSql.Replace("sde_schema", sdeSchemaName);

            if (oracle)
                cmdSql = cmdSql.Replace("SDE_", "");

            cmd = sdeConnection.CreateCommand();
            cmd.CommandText = cmdSql;

            // Execute the query
            rdr = cmd.ExecuteReader();

            while (rdr.Read())
            {
                string tableOwner = rdr.GetString(0);
                string tableName = rdr.GetString(1);
                string columnName = rdr.GetString(2);
                int sdeType = rdr.GetInt32(3);
                int columnSize = rdr.IsDBNull(4) ? 0 : rdr.GetInt32(4);

                SDEObject sdeObject = GetTableMetaData(tableOwner, tableName);
                if (sdeObject != null)
                {
                    SDEColumn column = new SDEColumn() { Name = columnName, SDEType = sdeType, Size = columnSize };
                    sdeObject.Columns.Add(column);
                }
            }

            rdr.Close();
        }

        /// <summary>
        /// Find SDE table metadata by owner and tablename
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public SDEObject GetTableMetaData(string owner, string tableName)
        {
            string key = (owner + "." + tableName).ToLower();

            if (_sdeObjects.ContainsKey(key))
                return _sdeObjects[key];
            else
                return null;
        }

        /// <summary>
        /// Find SDE table metadata by owner and tablename
        /// </summary>
        /// <param name="owner"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public SDEObject GetTableMetaData(string tableOwnerDotName)
        {
            string key = tableOwnerDotName.ToLower();

            if (_sdeObjects.ContainsKey(key))
                return _sdeObjects[key];
            else
                return null;
        }


        /// <summary>
        /// Find SDE table metadata by tablename.
        /// May return more than one table.
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public List<SDEObject> FindTableMetaData(string tableName)
        {
            List<SDEObject> sdeObjects = new List<SDEObject>();

            foreach (SDEObject sdeObject in _sdeObjects.Values)
            {
                if (sdeObject.TableName.ToLower() == tableName.ToLower())
                    sdeObjects.Add(sdeObject);
            }

            return sdeObjects;
        }
    }

    public class SDEObject
    {
        public int RegistrationID { get; set; }
        public int objectFlags { get; set; }
        public int srid { get; set; }
        public string Owner { get; set; }
        public string TableName { get; set; }
        public string IDColumnName { get; set; }
        public string GeometryColumnName { get; set; }
        public string AddTableName { get; set; }
        public string DeleteTableName { get; set; }
        public string FeatureTableName { get; set; }
        public double FalseX { get; set; }
        public double FalseY { get; set; }
        public double FalseZ { get; set; }
        public double XYUnits { get; set; }
        public double ZUnits { get; set; }
        public double MUnits { get; set; }
        public int GeometryType { get; set; }
        public int StorageType { get; set; }
        public int CoordDimension { get; set; }
        public List<SDEColumn> Columns = new List<SDEColumn>();
    }

    public class SDEColumn
    {
        public string Name { get; set; }
        public int SDEType { get; set; }
        public int Size { get; set; }
    }

    public static class SDEMultiViewHelper
    {
        public static string CreateSQL(SDEMetaData metaData, string tableName, string[] fieldNames, bool oracle = false)
        {
            List<SDEObject> sdeObjects = metaData.FindTableMetaData(tableName);

            if (sdeObjects != null && sdeObjects.Count > 0)
            {
                SDEObject sdeObject = sdeObjects[0];




                string templateSql = @"
                SELECT [b_fields] 
                FROM [b_tablename] b 
                LEFT HASH JOIN 
                (
                 SELECT SDE_DELETES_ROW_ID, SDE_STATE_ID
                 FROM [d_tablename]
                 WHERE SDE_STATE_ID = 0 AND DELETED_AT IN
                 (
                  SELECT l.lineage_id
                  FROM sde.SDE_states s 
                  INNER LOOP JOIN sde.SDE_state_lineages l ON l.lineage_name = s.lineage_name
                  WHERE s.state_id = sde.SDE_get_view_state() AND l.lineage_id <= s.state_id
                  )
                 ) d ON b.OBJECTID = d.SDE_DELETES_ROW_ID
                WHERE d.SDE_STATE_ID IS NULL
                UNION ALL
                SELECT [a_fields]
                FROM [a_tablename] a 
                LEFT HASH JOIN
                (
                 SELECT SDE_DELETES_ROW_ID, SDE_STATE_ID
                 FROM [d_tablename]
                 WHERE DELETED_AT IN
                 (
                  SELECT l.lineage_id
                  FROM sde.SDE_states s 
                  INNER LOOP JOIN sde.SDE_state_lineages l ON l.lineage_name = s.lineage_name
                  WHERE s.state_id = sde.SDE_get_view_state() AND l.lineage_id <= s.state_id
                  )
                 ) d ON (a.OBJECTID = d.SDE_DELETES_ROW_ID) AND (a.SDE_STATE_ID = d .SDE_STATE_ID)
                WHERE a.SDE_STATE_ID IN
                 (
                  SELECT l.lineage_id
                  FROM sde.SDE_states s 
                  INNER LOOP JOIN sde.SDE_state_lineages l ON l.lineage_name = s.lineage_name
                  WHERE s.state_id = sde.SDE_get_view_state() AND l.lineage_id <= s.state_id
                  ) 
                AND d .SDE_STATE_ID IS NULL";


                if (oracle)
                {
                    templateSql = @"
                    SELECT [b_fields] 
                    FROM [b_tablename] b ";

                    templateSql = @"SELECT * FROM 
                        ( SELECT [b_fields] 
                        FROM [b_tablename] b, (SELECT 
                            SDE_DELETES_ROW_ID, SDE_STATE_ID 
                          FROM 
                            [d_tablename] 
                          WHERE SDE_STATE_ID = 0 AND SDE.version_util.in_current_lineage (DELETED_AT) > 0 
                          ) d 
                        WHERE 
                          b.OBJECTID = d.SDE_DELETES_ROW_ID(+) AND d.SDE_STATE_ID IS NULL
                        UNION ALL
                        SELECT  [a_fields] 
                        FROM [a_tablename] a, (SELECT 
                            SDE_DELETES_ROW_ID, SDE_STATE_ID
                          FROM 
                            [d_tablename]
                          WHERE SDE.version_util.in_current_lineage (DELETED_AT) > 0
                          ) d
                        WHERE     
                          a.OBJECTID = d.SDE_DELETES_ROW_ID(+)
                          AND a.SDE_STATE_ID = d.SDE_STATE_ID(+)
                          AND SDE.version_util.in_current_lineage (a.SDE_STATE_ID) > 0
                          AND d.SDE_STATE_ID IS NULL ) mv ";
                }

                /*
                SELECT b.OBJECTID,
          b.STRUCTURE_NAME,
          b.CATEGORY_NAME,
          b.TYPE_NAME,
          b.LOCATION_CODE,
          b.OWNERSHIP_TYPE_CODE,
          b.INVENTORY_STATUS_CODE,
          b.PARENT_STRUCTURE_NAME,
          b.WORK_ORDER_NAME,
          b.WORK_ORDER_ITEM_NUMBER,
          b.STRUCTURE_REF_NAME,
          b.LENGTH_ADJUSTMENT,
          b.REMARKS,
          b.FIELD_NAME,
          b.ACCOUNT_CODE,
          b.PLACEMENT_DATE,
          b.HEIGHT,
          b.WIDTH,
          b.LENGTH,
          b.DEPTH_PLACED,
          b.AIR_COND_REQUIREMENT,
          b.POWER_TYPE_AVAILABLE,
          b.MAX_POWER_REQUIREMENT,
          b.CUSTOMER_NAME,
          b.CLLI_CODE,
          b.PURCHASE_ORDER,
          b.INSTALLED_COST,
          b.JOINT_USE,
          b.VOLUME,
          b.KEY_ACCESS_ID,
          b.SITE_TYPE,
          b.MODIFICATION_IND,
          b.DIAMETER,
          b.WEIGHT,
          b.MATERIAL_COST,
          b.MATERIAL_TYPE,
          b.ORDERABLE,
          b.UUID,
          b.MANUFACTURER_CODE,
          b.PRODUCT_LINE_CODE,
          b.PART_NUMBER,
          b.ANCILLARYROLE,
          b.ENABLED,
          b.ROTATION,
          b.MANAGED_OBJECT_ID,
          b.DI_TOKEN,
          b.GLOBAL_ID,
          b.GROUND_HEIGHT,
          b.MR,
          b.OBJECT_CLASS,
          b.TIME_STAMP,
          b.MOVED_IND,
          b.SHAPE,
          0 SDE_STATE_ID,
          b.MODEL_UUID,
          b.TERRAIN,
          b.NETTYPE,
          b.REMARKS_02,
          b.REMARKS_03,
          b.REMARKS_04,
          b.COLOUR,
          b.REF_UUID,
          b.FEAT_OPRET_DATO,
          b.FEAT_OPRET,
          b.ATTRIB_REDIG_DATO,
          b.ATTRIB_REDIG,
          b.SHP_REDIG_DATO,
          b.SHP_REDIG,
          b.UNDER_OPKLARING,
          b.DEFFEKT_SPLITTER_UDG,
          b.REGISTRERINGS_OPLYSNINGER,
          b.STRUCTURE_ID,
          b.AVERAGE_FLOOR_HEIGHT,
          b.STARTING_FLOOR,
          b.ENDING_FLOOR,
          b.UNITS,
          b.GROUND_FLOOR_NUMBER,
          b.FQN
     FROM NE.STRUCTURE b,
          (SELECT SDE_DELETES_ROW_ID, SDE_STATE_ID
             FROM NE.D34
            WHERE     SDE_STATE_ID = 0
                  AND SDE.version_util.in_current_lineage (DELETED_AT) > 0) d
    WHERE b.OBJECTID = d.SDE_DELETES_ROW_ID(+) AND d.SDE_STATE_ID IS NULL
   UNION ALL
   SELECT a.OBJECTID,
          a.STRUCTURE_NAME,
          a.CATEGORY_NAME,
          a.TYPE_NAME,
          a.LOCATION_CODE,
          a.OWNERSHIP_TYPE_CODE,
          a.INVENTORY_STATUS_CODE,
          a.PARENT_STRUCTURE_NAME,
          a.WORK_ORDER_NAME,
          a.WORK_ORDER_ITEM_NUMBER,
          a.STRUCTURE_REF_NAME,
          a.LENGTH_ADJUSTMENT,
          a.REMARKS,
          a.FIELD_NAME,
          a.ACCOUNT_CODE,
          a.PLACEMENT_DATE,
          a.HEIGHT,
          a.WIDTH,
          a.LENGTH,
          a.DEPTH_PLACED,
          a.AIR_COND_REQUIREMENT,
          a.POWER_TYPE_AVAILABLE,
          a.MAX_POWER_REQUIREMENT,
          a.CUSTOMER_NAME,
          a.CLLI_CODE,
          a.PURCHASE_ORDER,
          a.INSTALLED_COST,
          a.JOINT_USE,
          a.VOLUME,
          a.KEY_ACCESS_ID,
          a.SITE_TYPE,
          a.MODIFICATION_IND,
          a.DIAMETER,
          a.WEIGHT,
          a.MATERIAL_COST,
          a.MATERIAL_TYPE,
          a.ORDERABLE,
          a.UUID,
          a.MANUFACTURER_CODE,
          a.PRODUCT_LINE_CODE,
          a.PART_NUMBER,
          a.ANCILLARYROLE,
          a.ENABLED,
          a.ROTATION,
          a.MANAGED_OBJECT_ID,
          a.DI_TOKEN,
          a.GLOBAL_ID,
          a.GROUND_HEIGHT,
          a.MR,
          a.OBJECT_CLASS,
          a.TIME_STAMP,
          a.MOVED_IND,
          a.SHAPE,
          a.SDE_STATE_ID,
          a.MODEL_UUID,
          a.TERRAIN,
          a.NETTYPE,
          a.REMARKS_02,
          a.REMARKS_03,
          a.REMARKS_04,
          a.COLOUR,
          a.REF_UUID,
          a.FEAT_OPRET_DATO,
          a.FEAT_OPRET,
          a.ATTRIB_REDIG_DATO,
          a.ATTRIB_REDIG,
          a.SHP_REDIG_DATO,
          a.SHP_REDIG,
          a.UNDER_OPKLARING,
          a.DEFFEKT_SPLITTER_UDG,
          a.REGISTRERINGS_OPLYSNINGER,
          a.STRUCTURE_ID,
          a.AVERAGE_FLOOR_HEIGHT,
          a.STARTING_FLOOR,
          a.ENDING_FLOOR,
          a.UNITS,
          a.GROUND_FLOOR_NUMBER,
          a.FQN
     FROM NE.A34 a,
          (SELECT SDE_DELETES_ROW_ID, SDE_STATE_ID
             FROM NE.D34
            WHERE SDE.version_util.in_current_lineage (DELETED_AT) > 0) d
    WHERE     a.OBJECTID = d.SDE_DELETES_ROW_ID(+)
          AND a.SDE_STATE_ID = d.SDE_STATE_ID(+)
          AND SDE.version_util.in_current_lineage (a.SDE_STATE_ID) > 0
          AND d.SDE_STATE_ID IS NULL;
                */

                templateSql = templateSql.Replace("[b_tablename]", sdeObject.Owner + "." + sdeObject.TableName);
                templateSql = templateSql.Replace("[a_tablename]", sdeObject.Owner + "." + sdeObject.AddTableName);
                templateSql = templateSql.Replace("[d_tablename]", sdeObject.Owner + "." + sdeObject.DeleteTableName);

                string baseFields = "";
                string addFields = "";

                foreach (string fieldName in fieldNames)
                {
                    if (baseFields != "")
                        baseFields += ", ";

                    if (addFields != "")
                        addFields += ", ";


                    if (fieldName.IndexOf("(shape)") > -1)
                    {
                        baseFields += fieldName.Replace("(shape)", "(b.shape)");
                        addFields += fieldName.Replace("(shape)", "(a.shape)");
                    }
                    else
                    {
                        baseFields += "b." + fieldName;
                        addFields += "a." + fieldName;
                    }
                }


                templateSql = templateSql.Replace("[b_fields]", baseFields);
                templateSql = templateSql.Replace("[a_fields]", addFields);

                System.Diagnostics.Debug.WriteLine(templateSql);


                return templateSql;
            }

            return null;
        }
    }

    public enum SDEColumnType
    {
        SE_INT16 = 1,
        SE_INT32 = 2,
        SE_FLOAT32 = 3,
        SE_FLOAT64 = 4,
        SE_STRING = 5,
        SE_BLOB = 6,
        SE_DATE = 7,
        SE_SHAPE = 8,
        SE_RASTER = 9,
        SE_XML = 10,
        SE_INT64 = 11,
        SE_UUID = 12,
        SE_CLOB = 13,
        SE_NSTRING = 14
    }

    public enum SDEGeometryType
    {
        ST_GEOMETRY = 0,
        ST_POINT = 1,
        ST_CURVE = 2,
        ST_LINESTRING = 3,
        ST_SURFACE = 4,
        ST_POLYGON = 5,
        ST_COLLECTION = 6,
        ST_MULTIPOINT = 7,
        ST_MULTICURVE = 8,
        ST_MULTILINESTRING = 9,
        ST_MULTISURFACE = 10,
        ST_MULTIPOLYGON = 11
    }
}


