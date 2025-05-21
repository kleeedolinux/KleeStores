using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Threading;
using KleeStore.Models;

namespace KleeStore.Managers
{
    public class DatabaseManager
    {
        private static DatabaseManager? _instance;
        private static readonly object _lock = new object();
        
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly int _poolSize;
        private readonly Queue<SQLiteConnection> _connectionPool = new Queue<SQLiteConnection>();
        private int _activeConnections = 0;
        
        private DatabaseManager(string dbName = "kleestore.db", int poolSize = 5)
        {
            _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbName);
            _connectionString = $"Data Source={_dbPath};Version=3;";
            _poolSize = poolSize;
            
            
            for (int i = 0; i < poolSize; i++)
            {
                var conn = CreateConnection();
                if (conn != null)
                {
                    _connectionPool.Enqueue(conn);
                }
            }
            
            
            CreateTables();
        }
        
        public static DatabaseManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new DatabaseManager();
                    }
                }
                return _instance;
            }
        }
        
        private SQLiteConnection? CreateConnection()
        {
            try
            {
                var conn = new SQLiteConnection(_connectionString);
                conn.Open();
                return conn;
            }
            catch (Exception ex)
            {
                //console.WriteLine($"Error creating database connection: {ex.Message}");
                return null;
            }
        }
        
        private SQLiteConnection? GetConnection(int timeout = 5)
        {
            DateTime startTime = DateTime.Now;
            
            while ((DateTime.Now - startTime).TotalSeconds < timeout)
            {
                lock (_connectionPool)
                {
                    if (_connectionPool.Count > 0)
                    {
                        return _connectionPool.Dequeue();
                    }
                    
                    if (_activeConnections < _poolSize * 2)
                    {
                        _activeConnections++;
                        return CreateConnection();
                    }
                }
                
                Thread.Sleep(100);
            }
            
            
            //console.WriteLine("Database connection pool exhausted");
            return null;
        }
        
        private void ReleaseConnection(SQLiteConnection connection)
        {
            lock (_connectionPool)
            {
                if (_connectionPool.Count < _poolSize)
                {
                    _connectionPool.Enqueue(connection);
                }
                else
                {
                    if (_activeConnections > _poolSize)
                    {
                        _activeConnections--;
                        connection.Close();
                        connection.Dispose();
                    }
                    else
                    {
                        _connectionPool.Enqueue(connection);
                    }
                }
            }
        }
        
        private void CreateTables()
        {
            var conn = GetConnection();
            if (conn == null) return;
            
            try
            {
                using var cmd = new SQLiteCommand(conn);
                
                
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS packages (
                        id TEXT PRIMARY KEY,
                        name TEXT NOT NULL,
                        version TEXT NOT NULL,
                        description TEXT,
                        image_url TEXT,
                        install_command TEXT,
                        downloads INTEGER DEFAULT 0,
                        details_url TEXT,
                        is_installed INTEGER DEFAULT 0,
                        install_date TEXT
                    )";
                cmd.ExecuteNonQuery();
                
                
                cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_packages_name ON packages(name)";
                cmd.ExecuteNonQuery();
                
                cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_packages_downloads ON packages(downloads DESC)";
                cmd.ExecuteNonQuery();
                
                cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_packages_installed ON packages(is_installed, install_date DESC)";
                cmd.ExecuteNonQuery();
                
                //console.WriteLine("Database tables created");
            }
            catch (Exception ex)
            {
                //console.WriteLine($"Error creating tables: {ex.Message}");
            }
            finally
            {
                ReleaseConnection(conn);
            }
        }
        
        public bool AddOrUpdatePackage(Package package)
        {
            var conn = GetConnection();
            if (conn == null) return false;
            
            try
            {
                using var cmd = new SQLiteCommand(conn);
                
                
                cmd.CommandText = "SELECT 1 FROM packages WHERE id = @id";
                cmd.Parameters.AddWithValue("@id", package.Id);
                
                var exists = cmd.ExecuteScalar() != null;
                
                if (exists)
                {
                    
                    cmd.CommandText = @"
                        UPDATE packages 
                        SET name = @name, 
                            version = @version, 
                            description = @description, 
                            image_url = @imageUrl, 
                            install_command = @installCommand, 
                            downloads = @downloads, 
                            details_url = @detailsUrl 
                        WHERE id = @id";
                }
                else
                {
                    
                    cmd.CommandText = @"
                        INSERT INTO packages (
                            id, name, version, description, image_url, 
                            install_command, downloads, details_url
                        ) 
                        VALUES (
                            @id, @name, @version, @description, @imageUrl, 
                            @installCommand, @downloads, @detailsUrl
                        )";
                }
                
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@id", package.Id);
                cmd.Parameters.AddWithValue("@name", package.Name);
                cmd.Parameters.AddWithValue("@version", package.Version);
                cmd.Parameters.AddWithValue("@description", package.Description);
                cmd.Parameters.AddWithValue("@imageUrl", package.ImageUrl);
                cmd.Parameters.AddWithValue("@installCommand", package.InstallCommand);
                cmd.Parameters.AddWithValue("@downloads", package.Downloads);
                cmd.Parameters.AddWithValue("@detailsUrl", package.DetailsUrl);
                
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                //console.WriteLine($"Error adding/updating package: {ex.Message}");
                return false;
            }
            finally
            {
                ReleaseConnection(conn);
            }
        }
        
        public List<Package> SearchPackages(string query, int limit = 100, int offset = 0)
        {
            var result = new List<Package>();
            var conn = GetConnection();
            if (conn == null) return result;
            
            try
            {
                using var cmd = new SQLiteCommand(conn);
                
                cmd.CommandText = @"
                    SELECT * FROM packages 
                    WHERE name LIKE @query OR id LIKE @query OR description LIKE @query 
                    ORDER BY downloads DESC 
                    LIMIT @limit OFFSET @offset";
                
                cmd.Parameters.AddWithValue("@query", $"%{query}%");
                cmd.Parameters.AddWithValue("@limit", limit);
                cmd.Parameters.AddWithValue("@offset", offset);
                
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(ReadPackage(reader));
                }
            }
            catch (Exception ex)
            {
                //console.WriteLine($"Error searching packages: {ex.Message}");
            }
            finally
            {
                ReleaseConnection(conn);
            }
            
            return result;
        }
        
        public List<Package> GetAllPackages(int limit = 100, int offset = 0)
        {
            var result = new List<Package>();
            var conn = GetConnection();
            if (conn == null) return result;
            
            try
            {
                using var cmd = new SQLiteCommand(conn);
                
                cmd.CommandText = "SELECT * FROM packages ORDER BY downloads DESC LIMIT @limit OFFSET @offset";
                cmd.Parameters.AddWithValue("@limit", limit);
                cmd.Parameters.AddWithValue("@offset", offset);
                
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(ReadPackage(reader));
                }
            }
            catch (Exception ex)
            {
                //console.WriteLine($"Error getting packages: {ex.Message}");
            }
            finally
            {
                ReleaseConnection(conn);
            }
            
            return result;
        }
        
        public List<Package> GetInstalledPackages()
        {
            var result = new List<Package>();
            var conn = GetConnection();
            if (conn == null) return result;
            
            try
            {
                using var cmd = new SQLiteCommand(conn);
                
                cmd.CommandText = "SELECT * FROM packages WHERE is_installed = 1 ORDER BY install_date DESC";
                
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(ReadPackage(reader));
                }
            }
            catch (Exception ex)
            {
                //console.WriteLine($"Error getting installed packages: {ex.Message}");
            }
            finally
            {
                ReleaseConnection(conn);
            }
            
            return result;
        }
        
        public bool UpdatePackageInstallationStatus(string packageId, bool isInstalled, string? installDate = null)
        {
            var conn = GetConnection();
            if (conn == null) return false;
            
            try
            {
                using var cmd = new SQLiteCommand(conn);
                
                if (isInstalled && installDate != null)
                {
                    cmd.CommandText = "UPDATE packages SET is_installed = 1, install_date = @installDate WHERE id = @id";
                    cmd.Parameters.AddWithValue("@installDate", installDate);
                }
                else
                {
                    cmd.CommandText = "UPDATE packages SET is_installed = @isInstalled WHERE id = @id";
                    cmd.Parameters.AddWithValue("@isInstalled", isInstalled ? 1 : 0);
                }
                
                cmd.Parameters.AddWithValue("@id", packageId);
                
                cmd.ExecuteNonQuery();
                return true;
            }
            catch (Exception ex)
            {
                //console.WriteLine($"Error updating installation status: {ex.Message}");
                return false;
            }
            finally
            {
                ReleaseConnection(conn);
            }
        }
        
        public void BatchUpdateInstallationStatus(Dictionary<string, string> updates)
        {
            if (updates.Count == 0) return;
            
            var conn = GetConnection();
            if (conn == null) return;
            
            SQLiteTransaction? transaction = null;
            
            try
            {
                transaction = conn.BeginTransaction();
                
                using var cmd = new SQLiteCommand(conn);
                
                
                cmd.CommandText = "SELECT id FROM packages WHERE is_installed = 1";
                var existingInstalled = new HashSet<string>();
                
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        existingInstalled.Add(reader.GetString(0));
                    }
                }
                
                
                var toInstall = new HashSet<string>(updates.Keys);
                
                
                var toUninstall = new HashSet<string>(existingInstalled);
                toUninstall.ExceptWith(toInstall);
                
                
                foreach (var (packageId, installDate) in updates)
                {
                    cmd.Parameters.Clear();
                    
                    if (!string.IsNullOrEmpty(installDate))
                    {
                        cmd.CommandText = "UPDATE packages SET is_installed = 1, install_date = @installDate WHERE id = @id";
                        cmd.Parameters.AddWithValue("@installDate", installDate);
                    }
                    else
                    {
                        cmd.CommandText = "UPDATE packages SET is_installed = 1 WHERE id = @id";
                    }
                    
                    cmd.Parameters.AddWithValue("@id", packageId);
                    cmd.ExecuteNonQuery();
                }
                
                
                if (toUninstall.Count > 0)
                {
                    var idList = string.Join(",", toUninstall.Select(id => $"'{id}'"));
                    cmd.CommandText = $"UPDATE packages SET is_installed = 0 WHERE id IN ({idList})";
                    cmd.ExecuteNonQuery();
                }
                
                transaction.Commit();
            }
            catch (Exception ex)
            {
                //console.WriteLine($"Error in batch update: {ex.Message}");
                transaction?.Rollback();
            }
            finally
            {
                ReleaseConnection(conn);
            }
        }
        
        public int GetPackageCount()
        {
            var conn = GetConnection();
            if (conn == null) return 0;
            
            try
            {
                using var cmd = new SQLiteCommand(conn);
                
                cmd.CommandText = "SELECT COUNT(*) FROM packages";
                var result = cmd.ExecuteScalar();
                
                return result != null ? Convert.ToInt32(result) : 0;
            }
            catch (Exception ex)
            {
                //console.WriteLine($"Error getting package count: {ex.Message}");
                return 0;
            }
            finally
            {
                ReleaseConnection(conn);
            }
        }
        
        private Package ReadPackage(SQLiteDataReader reader)
        {
            var package = new Package
            {
                Id = reader["id"].ToString() ?? string.Empty,
                Name = reader["name"].ToString() ?? string.Empty,
                Version = reader["version"].ToString() ?? string.Empty,
                Description = reader["description"].ToString() ?? string.Empty,
                ImageUrl = reader["image_url"].ToString() ?? string.Empty,
                InstallCommand = reader["install_command"].ToString() ?? string.Empty,
                Downloads = reader["downloads"] != DBNull.Value ? Convert.ToInt32(reader["downloads"]) : 0,
                DetailsUrl = reader["details_url"].ToString() ?? string.Empty,
                IsInstalled = reader["is_installed"] != DBNull.Value && Convert.ToInt32(reader["is_installed"]) == 1,
            };
            
            if (reader["install_date"] != DBNull.Value)
            {
                var installDateStr = reader["install_date"].ToString();
                if (DateTime.TryParse(installDateStr, out var installDate))
                {
                    package.InstallDate = installDate;
                }
            }
            
            return package;
        }
        
        public void Close()
        {
            lock (_connectionPool)
            {
                while (_connectionPool.Count > 0)
                {
                    var conn = _connectionPool.Dequeue();
                    conn.Close();
                    conn.Dispose();
                }
            }
        }
    }
} 