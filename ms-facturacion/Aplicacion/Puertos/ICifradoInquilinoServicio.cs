using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

/// Puerto driven para la capacidad criptográfica (AES-256-GCM bajo una llave maestra) — análogo a un
/// cliente HTTP/S3 en AGENTS.md: un Adaptador de infraestructura que no es persistencia SQL directa.
public interface ICifradoInquilinoServicio
{
    Task<ResultadoOperacion<CredencialCifrada>> CifrarAsync(
        int idInquilino, string valorPlano, CancellationToken cancellationToken);

    Task<ResultadoOperacion<string>> DescifrarAsync(
        int idInquilino, byte[] valorCifrado, byte[] nonce, byte[] tag, CancellationToken cancellationToken);
}
