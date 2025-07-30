using ProgramaControl.Models;
using ProgramaControl.Utils;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProgramaControl.Services
{
    public class OperacionService
    {
        private readonly string _dbPath = @"C:\OperacionesPendientes\operaciones.db";

        public List<Operacion> ObtenerPendientes(string entorno)
        {
            var lista = new List<Operacion>();
            try
            {
                using var conn = new SQLiteConnection($"Data Source={_dbPath};");
                conn.Open();

                var cmd = new SQLiteCommand("SELECT * FROM operaciones WHERE estado = 'pendiente' AND entorno = @ent", conn);
                cmd.Parameters.AddWithValue("@ent", entorno);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    lista.Add(new Operacion
                    {
                        Id = reader.GetInt32(0),
                        Tipo = reader.GetString(1),
                        Nombre = reader.GetString(2),
                        Origen = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        Destino = reader.IsDBNull(4) ? "" : reader.GetString(4),
                        Entorno = reader.GetString(5),
                        Fecha = reader.GetString(6),
                        Estado = reader.GetString(7)
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error al leer operaciones: " + ex.Message);
            }

            return lista;
        }

        public void MarcarProcesada(int id, string estado)
        {
            try
            {
                using var conn = new SQLiteConnection($"Data Source={_dbPath};");
                conn.Open();
                var cmd = new SQLiteCommand("UPDATE operaciones SET estado = @est WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("@est", estado);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Logger.Log($"Error al actualizar operación #{id}: {ex.Message}");
            }
        }
        public List<Operacion> ObtenerBackupsProcesados(string entorno)
        {
            var lista = new List<Operacion>();

            using var conn = new SQLiteConnection($"Data Source={_dbPath};");
            conn.Open();

            string query = "SELECT id, nombre, destino, nvolumenorigen FROM operaciones WHERE tipo = 'backup' AND entorno = @entorno AND estado = 'procesado' ORDER BY fecha DESC";
            using var cmd = new SQLiteCommand(query, conn);
            cmd.Parameters.AddWithValue("@entorno", entorno);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new Operacion
                {
                    Id = reader.GetInt32(0),
                    Nombre = reader.GetString(1),
                    Destino = reader.GetString(2),
                    nvolumenorigen = reader.GetInt32(3)
                });
            }

            return lista;
        }
    }
}
