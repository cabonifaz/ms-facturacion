using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

public sealed class GenerarTxtSireRvieCasoDeUso(
    IDocumentoElectronicoRepositorio repositorio, IGeneradorSireRvieServicio generador)
{
    public async Task<ResultadoOperacion<ArchivoTxtSireRvie>> EjecutarAsync(
        int idInquilino, int idEmpresa, DateOnly periodo, CancellationToken cancellationToken)
    {
        var documentos = await repositorio.ListarParaSireRvieAsync(idInquilino, idEmpresa, periodo, cancellationToken);
        if (documentos.IdTipoMensaje != TipoMensaje.Exito || documentos.Datos is null)
        {
            return new ResultadoOperacion<ArchivoTxtSireRvie>(documentos.IdTipoMensaje, documentos.Mensaje, default);
        }

        if (documentos.Datos.Count == 0)
        {
            return ResultadoOperacion<ArchivoTxtSireRvie>.DeReglaDeNegocio(
                "No hay documentos Aceptados/AceptadoConObservaciones en ese período para exportar al RVIE.");
        }

        var contenido = generador.Construir(documentos.Datos);

        // Convención de nombre documentada en SIRE_RVIE_Estructura_Campos.md:
        // LE{RUC}{YYYYMM}{CodigoLibro=0014}{SubLibro=04}{OportunidadEnvio}{IndContenido}{IndMoneda}{IndIGV}{IndCierre}
        // RUC/período/CodigoLibro(Registro de Ventas 14)/SubLibro(04=RVIE) son fijos y conocidos con certeza.
        // OportunidadEnvio/IndContenido/IndMoneda/IndIGV/IndCierre solo están documentados vía UN ejemplo
        // (0002/1/1/1/2) — la spec no enumera qué otros valores toman ni cuándo cambian, así que se usan
        // esos mismos valores como default razonable (envío 0002, con datos, indicadores en 1, cierre=2)
        // en vez de inventar una regla no documentada. Si SUNAT rechaza el nombre, esto es lo primero a revisar.
        var ruc = documentos.Datos[0].EmpresaRuc;
        const string codigoLibro = "0014";
        const string subLibro = "04";
        const string oportunidadEnvio = "0002";
        const string indicadorContenido = "1";
        const string indicadorMoneda = "1";
        const string indicadorIgv = "1";
        const string indicadorCierre = "2";
        var nombreArchivo =
            $"LE{ruc}{periodo:yyyyMM}{codigoLibro}{subLibro}{oportunidadEnvio}" +
            $"{indicadorContenido}{indicadorMoneda}{indicadorIgv}{indicadorCierre}.txt";

        return ResultadoOperacion<ArchivoTxtSireRvie>.DeExito(
            "TXT SIRE RVIE generado correctamente.", new ArchivoTxtSireRvie(nombreArchivo, contenido));
    }
}
