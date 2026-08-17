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

        // Convención de nombre real de SUNAT para reemplazar la propuesta del RVIE — verificada contra la
        // imagen oficial "RVIE Estructura de Nombre" (cpe.sunat.gob.pe/estructura-de-archivos), Anexo N.° 3
        // de la RS 112-2021/SUNAT (modificada por RS 040-2022/SUNAT), y contrastada contra un nombre de
        // archivo RVIE real (LE2060112979620260600140400021112.txt, Safety Report S.A.C., junio 2026 — ver
        // SIRE_RVIE_Estructura_Campos.md), que confirma el valor real de LLLLLL y que NN se omite para
        // CC=02 (el ejemplo real termina en el indicador G, sin sufijo). Posiciones (1-based):
        //   01-02  LE       fijo
        //   03-13  RUC      RUC del emisor (11 dígitos)
        //   14-17  AAAA     año
        //   18-19  MM       mes
        //   20-21  DD       "00" fijo para RVIE (no aplica día)
        //   22-27  LLLLLL   identificador del libro — "140400" = Registro de Ventas e Ingresos, Formato 14.4
        //                    (no "140100": ese es el código del antiguo PLE Formato 14.1, libro distinto)
        //   28-29  CC       oportunidad de presentación: "02" = reemplaza la propuesta (lo que genera este caso de uso)
        //   30     O        indicador de operaciones: "1" = empresa/entidad operativa
        //   31     I        indicador de contenido: "1" con información / "0" sin información
        //   32     M        moneda: "1" = Soles (el registro se lleva en moneda contable, no en la moneda
        //                    original de cada comprobante — ver Preguntas Frecuentes SIRE, cpe.sunat.gob.pe/node/131)
        //   33     G        "2" fijo = generado por el nuevo sistema SIRE/RVIE
        //   34-35  NN       correlativo de ajustes posteriores — solo aplica a CC=03 (ajustes posteriores);
        //                    se omite para CC=02, igual que en el ejemplo real.
        // Reemplaza la convención anterior (LE+RUC+YYYYMM+"0014"+"04"+"0002"+...), que mezclaba el código de
        // libro real (recortado a "0014") con un "sub-libro"/"oportunidad de envío" inventados sin fuente.
        var ruc = documentos.Datos[0].EmpresaRuc;
        const string identificadorLibro = "140400";
        const string oportunidadPresentacion = "02"; // reemplaza la propuesta
        const string indicadorOperaciones = "1"; // empresa/entidad operativa
        var indicadorContenido = documentos.Datos.Count > 0 ? "1" : "0";
        const string indicadorMoneda = "1"; // Soles
        const string indicadorGenerador = "2"; // fijo, nuevo sistema SIRE/RVIE
        var nombreArchivo =
            $"LE{ruc}{periodo:yyyyMM}00{identificadorLibro}{oportunidadPresentacion}" +
            $"{indicadorOperaciones}{indicadorContenido}{indicadorMoneda}{indicadorGenerador}.txt";

        return ResultadoOperacion<ArchivoTxtSireRvie>.DeExito(
            "TXT SIRE RVIE generado correctamente.", new ArchivoTxtSireRvie(nombreArchivo, contenido));
    }
}
