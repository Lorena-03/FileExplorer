namespace FileExplorer.Forms
{
    /// <summary>Motor de base de datos soportado.</summary>
    public enum DbEngine { SqlServer, MariaDB }

    /// <summary>
    /// Opciones de conexión y migración ingresadas por el usuario en la UI.
    /// Construye las cadenas de conexión dinámicamente — sin credenciales en el código.
    /// </summary>
    public class MigrationOptions
    {
        public DbEngine Engine      { get; set; } = DbEngine.SqlServer;
        public string   Host        { get; set; } = "";
        public int      Port        { get; set; } = 1433;
        public string   Database    { get; set; } = "";
        public string   User        { get; set; } = "";
        public string   Password    { get; set; } = "";
        public string   TableName   { get; set; } = "archivos";
        public bool     DropIfExists { get; set; } = true;
        public bool     CreateTable  { get; set; } = true;

        /// <summary>
        /// Construye la cadena de conexión a la base de datos de trabajo
        /// usando los valores ingresados por el usuario en la interfaz.
        /// </summary>
        public string BuildConnectionString() => Engine switch
        {
            DbEngine.SqlServer =>
                $"Server={Host},{Port};Database={Database};User Id={User};Password={Password};TrustServerCertificate=True;",
            DbEngine.MariaDB =>
                $"Server={Host};Port={Port};Database={Database};User={User};Password={Password};",
            _ => throw new NotSupportedException()
        };

        /// <summary>
        /// Construye la cadena de conexión a la base de datos maestra (master/mysql)
        /// para crear la base de datos destino si no existe.
        /// </summary>
        public string BuildMasterConnectionString() => Engine switch
        {
            DbEngine.SqlServer =>
                $"Server={Host},{Port};Database=master;User Id={User};Password={Password};TrustServerCertificate=True;",
            DbEngine.MariaDB =>
                $"Server={Host};Port={Port};Database=mysql;User={User};Password={Password};",
            _ => throw new NotSupportedException()
        };
    }
}
