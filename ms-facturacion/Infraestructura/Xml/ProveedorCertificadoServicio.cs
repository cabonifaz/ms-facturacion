using System.Security.Cryptography.X509Certificates;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Infraestructura.Xml;

/// Depende directamente de los mismos dos puertos que DescifrarCredencialPorTipoCasoDeUso usa
/// (ICredencialInquilinoRepositorio + ICifradoInquilinoServicio) en vez de depender de ese Caso de Uso:
/// un Adaptador de Infraestructura no debe depender de clases concretas de Aplicacion, solo de Puertos.
public sealed class ProveedorCertificadoServicio(
    ICertificadoRepositorio certificadoRepositorio,
    ICredencialInquilinoRepositorio credencialRepositorio,
    ICifradoInquilinoServicio cifradoServicio) : IProveedorCertificadoServicio
{
    private const string TipoCredencialClaveCertificado = "ClaveCertificado";

    public async Task<ResultadoOperacion<X509Certificate2>> ObtenerAsync(
        int idInquilino, int idEmpresa, int idCertificado, CancellationToken cancellationToken)
    {
        var certificado = await certificadoRepositorio.ObtenerAsync(idInquilino, idCertificado, cancellationToken);
        if (certificado.IdTipoMensaje != TipoMensaje.Exito || certificado.Datos is null)
        {
            return new ResultadoOperacion<X509Certificate2>(certificado.IdTipoMensaje, certificado.Mensaje, default);
        }

        var credencial = await credencialRepositorio.ObtenerPorTipoAsync(
            idInquilino, idEmpresa, TipoCredencialClaveCertificado, cancellationToken);
        if (credencial.IdTipoMensaje != TipoMensaje.Exito || credencial.Datos is null)
        {
            return new ResultadoOperacion<X509Certificate2>(credencial.IdTipoMensaje, credencial.Mensaje, default);
        }

        var clave = await cifradoServicio.DescifrarAsync(
            idInquilino, credencial.Datos.ValorCifrado, credencial.Datos.Nonce, credencial.Datos.Tag, cancellationToken);
        if (clave.IdTipoMensaje != TipoMensaje.Exito || clave.Datos is null)
        {
            return new ResultadoOperacion<X509Certificate2>(clave.IdTipoMensaje, clave.Mensaje, default);
        }

        var x509 = X509CertificateLoader.LoadPkcs12FromFile(
            certificado.Datos.RutaAlmacenamiento, clave.Datos,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);

        return ResultadoOperacion<X509Certificate2>.DeExito("Certificado cargado correctamente.", x509);
    }
}
