using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Configuration;

namespace DatabaseScanner
{
    class Program
    {
        static void Main(string[] args)
        {
            string configFilePath = args[0];
            string dbSizeCutOff = "0";
            if (args.Length == 2)
                dbSizeCutOff = args[1];
            GenerateReport(configFilePath, dbSizeCutOff);
            
        }

        private static SqlConnection connectToDb(ConnectionString connectionParams)
        {
            string connectionString = "Trusted_Connection=yes; connection timeout=30; database=" + connectionParams.database + ";server= " + connectionParams.server;
            if (connectionParams.userId != "")
                connectionString += ";user id=" + connectionParams.userId;
            if (connectionParams.password != "")
                connectionString += ";password=" + connectionParams.password;

            SqlConnection sqlConnection = new SqlConnection(connectionString);
            return sqlConnection;
        }

        public static void GenerateReport(string configFilePath, string dbSizeCutOff)
        {
            List<ConnectionString> serverList = ConnectionString.getConnectionStringsList(configFilePath);
            List<QueryOutputTuple> queryOutputTuples = new List<QueryOutputTuple>();
            int noOfQueries = Int32.Parse(ConfigurationManager.AppSettings["noOfQueries"]);
            for (int i = 1; i <= noOfQueries; i++)
            {
                QueryOutputTuple qot = new QueryOutputTuple(ConfigurationManager.AppSettings["query"+i], ConfigurationManager.AppSettings["queryOutputFile"+i]);
                queryOutputTuples.Add(qot);
            }

            foreach (ConnectionString cnStr in serverList)
            {
                Console.WriteLine("\nServer: "+ cnStr.server+"\n");
                List<string> dbList = GetDatabaseList(cnStr.server);

                foreach (string db in dbList)
                {
                    cnStr.database = db;
                    SqlConnection sqlConnection = connectToDb(cnStr);
                    try
                    {
                        if (tryOpeningSqlConnection(sqlConnection) )
                        {
                            foreach (QueryOutputTuple qot in queryOutputTuples)
                            {
                                SqlDataReader myReader = null;
                                string query = qot.query;
                                SqlCommand myCommand = new SqlCommand(query, sqlConnection);
                                myReader = myCommand.ExecuteReader();
                                writeTableToFile(myReader, qot.file, (cnStr.server+","+ db+","));
                                myReader.Close();
                            }
                            sqlConnection.Close();
                        }

                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.ToString());
                    }
                }
            }

        }

        private static void writeTableToFile(SqlDataReader myReader, string file, string prefix, string separator=",")
        {
            System.IO.StreamWriter sw = new System.IO.StreamWriter(file, true);
            while (myReader.Read())
            {
                sw.Write(prefix);
                for (int i = 0; i < myReader.FieldCount-1; i++)
                {
                    sw.Write(myReader[i].ToString() + separator);
                }
                sw.WriteLine(myReader[myReader.FieldCount - 1]);
            }
            sw.Close();
        }

        public static bool tryOpeningSqlConnection(SqlConnection sqlConnection)
        {
            try
            {
                sqlConnection.Open();
            }
            catch
            {
                Console.WriteLine("Can't connect to server: " + sqlConnection.DataSource +" db: " + sqlConnection.Database);
                return false;
            }
            return true;
        }
        public static List<string> GetDatabaseList(string server)
        {
            List<string> list = new List<string>();
            List<string> exclusionList = new List<string> { "master", "tempdb", "model", "msdb"};

            // Open connection to the database Yo!Yo!
            ConnectionString cnStr = new ConnectionString();
            cnStr.server = server;
            using (SqlConnection sqlConnection = connectToDb(cnStr))
            {
                if (tryOpeningSqlConnection(sqlConnection))
                {
                    // Set up a command with the given query and associate
                    // this with the current connection.
                    using (SqlCommand cmd = new SqlCommand("SELECT name from sys.databases", sqlConnection))
                    {
                        using (SqlDataReader dr = cmd.ExecuteReader())
                        {
                            while (dr.Read())
                            {
                                if(!exclusionList.Contains(dr[0]))
                                list.Add(dr[0].ToString());
                            }
                        }
                    }
                    sqlConnection.Close();
                }
            }
            return list;
        }

    }

    public struct QueryOutputTuple
    {
        public string query;
        public string file;

        public QueryOutputTuple(string v1, string v2) : this()
        {
            this.query = v1;
            this.file = v2;
        }
    }
    public class ConnectionString
    {
        public string server;
        public string userId = "";
        public string password = "";
        public string database = "master";

        public ConnectionString()
        {

        }
        public ConnectionString(string configLine)
        {
            string[] elements = configLine.Split('\t');

            server = elements[0].Trim();

            if (elements.Length > 1)
            {
                userId = elements[1].Trim();
                password = elements[2].Trim();
            }
            if (elements.Length == 4)
            {
                database = elements[3].Trim();
            }

        }

        public static List<ConnectionString> getConnectionStringsList(string configFile)
        {
            List<ConnectionString> conStrs = new List<ConnectionString>();
            string line;

            // Read the file and display it line by line.
            System.IO.StreamReader file =
               new System.IO.StreamReader(configFile);
            while ((line = file.ReadLine()) != null)
            {
                if (!line.StartsWith("#")) //comments
                    conStrs.Add(new ConnectionString(line));
            }

            file.Close();
            return conStrs;
        }
    }
}
