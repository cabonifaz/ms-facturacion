using System.Security.Cryptography.X509Certificates;
using ms_facturacion.Aplicacion.Comun;

namespace ms_facturacion.Aplicacion.Puertos;

/// Carga el certificado (ya resuelto por Id vía CONFIGURACIONES_FACTURACION_EMPRESA.IdCertificado) con su
/// clave privada (ClaveCertificado, descifrada vía el mismo mecanismo AES-GCM del Módulo 2).
public interface IProveedorCertificadoServicio
{
    Task<ResultadoOperacion<X509Certificate2>> ObtenerAsync(
        int idInquilino, int idEmpresa, int idCertificado, CancellationToken cancellationToken);
}
