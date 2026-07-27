using System.IO.Compression;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Infraestructura.Xml;

public sealed class EmpaquetadorZipServicio : IEmpaquetadorZipServicio
{
    public byte[] Empaquetar(string nombreArchivoXml, byte[] xmlFirmado)
    {
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entrada = zip.CreateEntry(nombreArchivoXml, CompressionLevel.Optimal);
            using var entradaStream = entrada.Open();
            entradaStream.Write(xmlFirmado, 0, xmlFirmado.Length);
        }

        return memoria.ToArray();
    }
}
