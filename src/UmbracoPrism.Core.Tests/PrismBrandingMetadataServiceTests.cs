using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using UmbracoPrism.Core.Services;

namespace UmbracoPrism.Core.Tests;

public class PrismBrandingMetadataServiceTests
{
    private readonly Mock<IWebHostEnvironment> _mockWebHostEnvironment;
    private readonly IMemoryCache _memoryCache;

    public PrismBrandingMetadataServiceTests()
    {
        _mockWebHostEnvironment = new Mock<IWebHostEnvironment>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
    }

    [Fact]
    public void GetBrandingMetadata_ReturnsEmptyList_WhenBrandingDirectoryDoesNotExist()
    {
        // Arrange
        _mockWebHostEnvironment.Setup(x => x.WebRootPath).Returns("/nonexistent");
        var service = new PrismBrandingMetadataService(_mockWebHostEnvironment.Object, _memoryCache);

        // Act
        var result = service.GetBrandingMetadata();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseCssFile_ExtractsVariableWithPrismAnnotation()
    {
        // Arrange
        var css = @"
@property --prism-primary {
  syntax: '<color>';
  inherits: true;
  initial-value: #4f46e5;
}

:root {
  /* @prism section: Brand Colours | label: Primary Brand Colour | description: Used for buttons and links */
  --prism-primary: #4f46e5;
}";

        var service = CreateServiceWithTestCss(css, "test.css");

        // Act
        var result = service.GetBrandingMetadata().ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Brand Colours");
        result[0].Variables.Should().HaveCount(1);
        
        var variable = result[0].Variables[0];
        variable.Variable.Should().Be("--prism-primary");
        variable.Label.Should().Be("Primary Brand Colour");
        variable.Type.Should().Be("color");
        variable.Syntax.Should().Be("<color>");
        variable.CurrentValue.Should().Be("#4f46e5");
    }

    [Fact]
    public void ParseCssFile_InfersTypeFromPropertySyntax_WhenTypeNotSpecified()
    {
        // Arrange
        var css = @"
@property --prism-spacing {
  syntax: '<length>';
  inherits: true;
}

:root {
  /* @prism section: Layout | label: Base Spacing */
  --prism-spacing: 1rem;
}";

        var service = CreateServiceWithTestCss(css, "test.css");

        // Act
        var result = service.GetBrandingMetadata().ToList();

        // Assert
        var variable = result[0].Variables[0];
        variable.Type.Should().Be("length");
        variable.Syntax.Should().Be("<length>");
    }

    [Fact]
    public void ParseCssFile_UsesExplicitTypeOverride_WhenProvided()
    {
        // Arrange
        var css = @"
@property --prism-logo {
  syntax: '<url>';
  inherits: true;
}

:root {
  /* @prism section: Imagery | label: Logo Image | type: image */
  --prism-logo: url('/logo.png');
}";

        var service = CreateServiceWithTestCss(css, "test.css");

        // Act
        var result = service.GetBrandingMetadata().ToList();

        // Assert
        var variable = result[0].Variables[0];
        variable.Type.Should().Be("image");
        variable.Syntax.Should().Be("<url>");
    }

    [Fact]
    public void ParseCssFile_GroupsVariablesBySection()
    {
        // Arrange
        var css = @"
:root {
  /* @prism section: Colors | label: Primary */
  --prism-primary: #4f46e5;
  
  /* @prism section: Typography | label: Font Family */
  --prism-font: Arial;
  
  /* @prism section: Colors | label: Secondary */
  --prism-secondary: #22c55e;
}";

        var service = CreateServiceWithTestCss(css, "test.css");

        // Act
        var result = service.GetBrandingMetadata().ToList();

        // Assert
        result.Should().HaveCount(2);
        
        var colorsSection = result.First(s => s.Name == "Colors");
        colorsSection.Variables.Should().HaveCount(2);
        colorsSection.Variables.Select(v => v.Variable).Should().Contain(new[] { "--prism-primary", "--prism-secondary" });
        
        var typographySection = result.First(s => s.Name == "Typography");
        typographySection.Variables.Should().HaveCount(1);
        typographySection.Variables[0].Variable.Should().Be("--prism-font");
    }

    [Fact]
    public void ParseCssFile_DefaultsToGeneralSection_WhenSectionNotSpecified()
    {
        // Arrange
        var css = @"
:root {
  /* @prism label: Some Variable */
  --prism-variable: value;
}";

        var service = CreateServiceWithTestCss(css, "test.css");

        // Act
        var result = service.GetBrandingMetadata().ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("General");
        result[0].Variables.Should().HaveCount(1);
    }

    [Fact]
    public void ParseCssFile_HandlesVariablesWithoutPropertyDeclarations()
    {
        // Arrange
        var css = @"
:root {
  /* @prism section: Custom | label: Custom Variable | type: text */
  --prism-custom: some-value;
}";

        var service = CreateServiceWithTestCss(css, "test.css");

        // Act
        var result = service.GetBrandingMetadata().ToList();

        // Assert
        var variable = result[0].Variables[0];
        variable.Variable.Should().Be("--prism-custom");
        variable.Type.Should().Be("text");
        variable.Syntax.Should().BeNull();
    }

    [Fact]
    public void ParseCssFile_InfersUrlTypeFromSyntax()
    {
        // Arrange
        var css = @"
@property --prism-background {
  syntax: '<url>';
  inherits: true;
}

:root {
  /* @prism section: Imagery | label: Background */
  --prism-background: url('/bg.jpg');
}";

        var service = CreateServiceWithTestCss(css, "test.css");

        // Act
        var result = service.GetBrandingMetadata().ToList();

        // Assert
        var variable = result[0].Variables[0];
        variable.Type.Should().Be("url");
    }

    [Fact]
    public void ParseCssFile_InfersTextTypeFromWildcardSyntax()
    {
        // Arrange
        var css = @"
@property --prism-text-value {
  syntax: '*';
  inherits: true;
}

:root {
  /* @prism section: Text | label: Text Value */
  --prism-text-value: some text;
}";

        var service = CreateServiceWithTestCss(css, "test.css");

        // Act
        var result = service.GetBrandingMetadata().ToList();

        // Assert
        var variable = result[0].Variables[0];
        variable.Type.Should().Be("text");
    }

    [Fact]
    public void ParseCssFile_MaintainsSectionOrderByFirstAppearance()
    {
        // Arrange
        var css = @"
:root {
  /* @prism section: Z Section | label: Last */
  --prism-last: value1;
  
  /* @prism section: A Section | label: First */
  --prism-first: value2;
  
  /* @prism section: Z Section | label: Another Last */
  --prism-another: value3;
}";

        var service = CreateServiceWithTestCss(css, "test.css");

        // Act
        var result = service.GetBrandingMetadata().ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Z Section");
        result[1].Name.Should().Be("A Section");
    }

    [Fact]
    public void ParseCssFile_HandlesMultipleAnnotationFields()
    {
        // Arrange
        var css = @"
@property --prism-accent {
  syntax: '<color>';
  inherits: true;
}

:root {
  /* @prism section: Colors | label: Accent Color | description: Used for highlights and call-to-actions | type: color */
  --prism-accent: #22c55e;
}";

        var service = CreateServiceWithTestCss(css, "test.css");

        // Act
        var result = service.GetBrandingMetadata().ToList();

        // Assert
        var variable = result[0].Variables[0];
        variable.Variable.Should().Be("--prism-accent");
        variable.Label.Should().Be("Accent Color");
        variable.Description.Should().Contain("description: Used for highlights and call-to-actions");
        variable.Type.Should().Be("color");
    }

    [Fact]
    public void GetBrandingMetadata_CachesResults()
    {
        // Arrange
        var css = @"
:root {
  /* @prism section: Test | label: Test Var */
  --prism-test: value;
}";

        var service = CreateServiceWithTestCss(css, "test.css");

        // Act
        var result1 = service.GetBrandingMetadata();
        var result2 = service.GetBrandingMetadata();

        // Assert
        result1.Should().BeSameAs(result2);
    }

    private PrismBrandingMetadataService CreateServiceWithTestCss(string cssContent, string filename)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var brandingDir = Path.Combine(tempDir, "branding");
        Directory.CreateDirectory(brandingDir);

        File.WriteAllText(Path.Combine(brandingDir, filename), cssContent);

        _mockWebHostEnvironment.Setup(x => x.WebRootPath).Returns(tempDir);

        return new PrismBrandingMetadataService(_mockWebHostEnvironment.Object, _memoryCache);
    }
}
