using PdfSharpCore.Fonts;
using PdfSharpCore.Utils;
using System.IO;
using System.Reflection;

namespace Kflmulti
{
    public class CustomFontResolver : IFontResolver
    {
        // Nome padrão da fonte que será usada caso não seja especificado
        public string DefaultFontName => "OpenSans-Regular";

        public byte[] GetFont(string faceName)
        {
            var assembly = typeof(CustomFontResolver).GetTypeInfo().Assembly;
            using (var stream = assembly.GetManifestResourceStream("Kflmulti.Resources.Fonts.OpenSans-Regular.ttf"))


            {
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }

        }

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // Aqui você pode diferenciar bold/italic se quiser
            return new FontResolverInfo("Arial");
        }
    }
}

