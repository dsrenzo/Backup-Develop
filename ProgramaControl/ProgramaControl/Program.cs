using ProgramaControl.Models;
using ProgramaControl.Services;
using ProgramaControl.Utils;
using ProgramaControl.Services.Ejecutores;

var entorno = EntornoDetector.Detectar();

var servicio = new OperacionService();
var operaciones = servicio.ObtenerPendientes(entorno);

foreach (var op in operaciones)
{
    try
    {
        Logger.Log($"Ejecutando operación #{op.Id}: {op.Tipo}");

        switch (op.Tipo.ToLower())
        {
            case "backup":
                BackupExecutor.Ejecutar(op);
                break;
            case "restore":
                RestoreExecutor.Ejecutar(op);
                break;
            case "delete":
                DeleteExecutor.Ejecutar(op);
                break;
            case "boot":
                BootExecutor.Ejecutar(op);
                break;
            case "shutdown":
                ShutdownExecutor.Ejecutar();
                break;
            default:
                throw new Exception($"Tipo de operación no soportado: {op.Tipo}");
        }

        servicio.MarcarProcesada(op.Id, "exitoso");
        Logger.Log($"Operación #{op.Id} completada con éxito.");
    }
    catch (Exception ex)
    {
        servicio.MarcarProcesada(op.Id, "fallido");
        Logger.Log($"Error en operación #{op.Id}: {ex.Message}");
    }
}
