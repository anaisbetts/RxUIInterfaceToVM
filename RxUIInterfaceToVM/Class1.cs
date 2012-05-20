using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ReactiveUI;
using Roslyn.Compilers.CSharp;
using Xunit;

namespace RxUIInterfaceToVM
{
    public class ViewModelRendererTests : IEnableLogger
    {
        [Fact]
        public void ShouldThrowOnEmptyStringOrGarbage()
        {
            var inputs = new[] {
                "###woefowaefjawioefj",
                "",
            };

            inputs.ForEach(x => {
                Assert.Throws<ArgumentException>(() => {
                    var fixture = new ViewModelRenderer();
                    fixture.RenderViewModel(x);
                });
            });
        }

        [Fact]
        public void ParseInterfacesSmokeTest()
        {
            var fixture = new ViewModelRenderer();

            string result = fixture.RenderViewModel(File.ReadAllText(@"C:\Users\Administrator\Documents\GitHub\RxUIInterfaceToVM\TestInterface.cs"));
            this.Log().Info(result);

            Assert.Contains("ReactiveObject", result);
            Assert.Contains("ObservableAsPropertyHelper", result);
            Assert.Contains("HostScreen", result);
        }
    }

    public class ViewModelRenderer : IEnableLogger
    {
        public string RenderViewModel(string interfaceCode)
        {
            var tree = SyntaxTree.ParseCompilationUnit(interfaceCode, "foo.cs");
            var root = tree.GetRoot(new CancellationToken());

            if (!root.ChildNodes().Any()) {
                throw new ArgumentException("Compilation failed or code is badly formatted");
            }

            if (!root.ChildNodes().All(x => x is InterfaceDeclarationSyntax)) {
                throw new ArgumentException("Code must be one ore more interfaces");
            }

            var renderInfo = root.ChildNodes()
                .OfType<InterfaceDeclarationSyntax>()
                .Select(renderInterface)
                .ToArray();

            var template = File.ReadAllText(@"C:\Users\Administrator\Documents\GitHub\RxUIInterfaceToVM\Template.mustache");
            var ret = Nustache.Core.Render.StringToString(template, new { interfaces = renderInfo });

            tree = SyntaxTree.ParseCompilationUnit(ret, "foo.cs");
            return tree.Root.Format().ToString();
        }

        InterfaceRenderInformation renderInterface(InterfaceDeclarationSyntax interfaceDecl)
        {
            var ret = new InterfaceRenderInformation();

            ret.isRoutableViewModel = interfaceDecl.BaseListOpt.Types.Any(x => x.PlainName == "IRoutableViewModel") ? this : null;
            ret.definition = chompedString(interfaceDecl.ToString().Replace("[Once]", ""));
            ret.interfaceName = chompedString(interfaceDecl.Identifier.ValueText);
            ret.implClassName = ret.interfaceName.Substring(1); // Skip the 'I'

            var children = interfaceDecl.ChildNodes().Skip(1);

            ret.properties = children.Select(renderPropertyDeclaration).ToArray();
            ret.onceProperties = ret.properties.Where(x => x.onceProp != null).Select(x => x.onceProp).ToArray();
            return ret;
        }

        PropertyRenderInformation renderPropertyDeclaration(SyntaxNode node)
        {
            var propDecl = node as PropertyDeclarationSyntax;
            if (propDecl == null) {
                return new PropertyRenderInformation() {
                    anythingElse = new NameAndTypeRenderInformation() { name = chompedString(node.ToString()) },
                };
            }

            var nameAndType = new NameAndTypeRenderInformation() {
                name = chompedString(propDecl.Identifier.ValueText), 
                type = chompedString(propDecl.Type.PlainName),
            };

            var commands = new[] {
                "ReactiveCommand",
                "ReactiveAsyncCommand",
            };

            if (propDecl.Attributes.SelectMany(x => x.Attributes).Any(x => x.Name.PlainName == "Once") ||
                commands.Contains(propDecl.Type.PlainName)) {
                return new PropertyRenderInformation() { onceProp = nameAndType, };
            }

            if (propDecl.AccessorList.Accessors.Any(x => x.Keyword.Kind == SyntaxKind.SetKeyword)) {
                return new PropertyRenderInformation() { readWriteProp = nameAndType, };
            } else {
                return new PropertyRenderInformation() { outputProp = nameAndType, };
            }
        }

        string chompedString(string code)
        {
            if (!code.Contains("\n")) {
                return code.TrimEnd(' ', '\t');
            }

            var lines = code.Split('\n')
                .Select(x => x.TrimEnd(' ', '\t'))
                .Where(x => !(String.IsNullOrWhiteSpace(x) && x.Length > 2));

            return String.Join("\n", lines);
        }
    }

    public class InterfaceRenderInformation
    {
        public IEnumerable<PropertyRenderInformation> properties { get; set; }
        public string definition { get; set; }
        public string implClassName { get; set; }
        public string interfaceName { get; set; }
        public object isRoutableViewModel { get; set; } // 'this' if true, null if false, Mustacheism

        public IEnumerable<NameAndTypeRenderInformation> onceProperties { get; set; }
    }

    public class PropertyRenderInformation
    {
        // NB: Only *one* of these should be non-null, Mustacheism
        public NameAndTypeRenderInformation outputProp { get; set; }
        public NameAndTypeRenderInformation onceProp { get; set; }
        public NameAndTypeRenderInformation readWriteProp { get; set; }
        public NameAndTypeRenderInformation anythingElse { get; set; }
    }

    public class NameAndTypeRenderInformation
    {
        public string name { get; set; }
        public string type { get; set; }
    }
}
