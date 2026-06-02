private string GetHSFromType(string componentBatch)
        {
            string sql = @"select spec from v_fw_material_indetail where sublot='"+componentBatch+"'";
            OracleConnection conn = new OracleConnection(ORACON);
            conn.Open();
            using (OracleCommand command = new OracleCommand(sql, conn))
            {


                object resultq = command.ExecuteScalar();

                if (resultq == null || resultq == DBNull.Value)
                {
                    ShowAutoCloseMessage("请扫描空轴焊丝物料号","错误");
                }
                conn.Close();
                return resultq.ToString();
            }
        }