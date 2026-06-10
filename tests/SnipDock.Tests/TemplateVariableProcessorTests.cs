using System.Collections.Generic;
using SnipDock.Core.Utils;
using Xunit;

namespace SnipDock.Tests
{
    public class TemplateVariableProcessorTests
    {
        [Fact]
        public void ExtractVariables_ReturnsUniqueVariablesInOrder()
        {
            var variables = TemplateVariableProcessor.ExtractVariables(
                "Write for {{name}} in {{style}}. Repeat {{ name }} and {{content}}.");

            Assert.Collection(
                variables,
                variable => Assert.Equal("name", variable.Name),
                variable => Assert.Equal("style", variable.Name),
                variable => Assert.Equal("content", variable.Name));
        }

        [Fact]
        public void ExtractVariables_IgnoresInvalidPatterns()
        {
            var variables = TemplateVariableProcessor.ExtractVariables(
                "{{ valid_name }} {{1bad}} {{bad-name}} {missing}");

            Assert.Single(variables);
            Assert.Equal("valid_name", variables[0].Name);
        }

        [Fact]
        public void Render_ReplacesVariablesWithProvidedValues()
        {
            var result = TemplateVariableProcessor.Render(
                "Hello {{name}}, use {{ style }}.",
                new Dictionary<string, string?> { ["name"] = "Alex", ["style"] = "concise" });

            Assert.Equal("Hello Alex, use concise.", result);
        }

        [Fact]
        public void Render_AllowsEmptyValues()
        {
            var result = TemplateVariableProcessor.Render(
                "Hello {{name}}.",
                new Dictionary<string, string?> { ["name"] = string.Empty });

            Assert.Equal("Hello .", result);
        }

        [Fact]
        public void Render_LeavesMissingValuesUnchanged()
        {
            var result = TemplateVariableProcessor.Render(
                "Hello {{name}} and {{unknown}}.",
                new Dictionary<string, string?> { ["name"] = "Alex" });

            Assert.Equal("Hello Alex and {{unknown}}.", result);
        }

        [Fact]
        public void HasVariables_ReturnsExpectedState()
        {
            Assert.True(TemplateVariableProcessor.HasVariables("Run {{command}}"));
            Assert.False(TemplateVariableProcessor.HasVariables("Run command"));
            Assert.False(TemplateVariableProcessor.HasVariables(null));
        }
    }
}
