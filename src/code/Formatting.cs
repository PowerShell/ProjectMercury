using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Microsoft.PowerShell.Copilot
{
    internal class Formatting
    {
        internal static string GetPrettyPowerShellScript(string script)
        {
            // parse the script to ast
            var ast = Parser.ParseInput(script, out Token[] tokens, out ParseError[] errors);
            // walk through the tokens and color them depending on type
            var sb = new StringBuilder();
            var colorTokens = new List<(int, string)>(); // (start, color)
            foreach (var token in tokens)
            {
                var color = PSStyle.Instance.Foreground.White;
                switch (token.Kind)
                {
                    case TokenKind.Command:
                        color = PSStyle.Instance.Foreground.BrightYellow;
                        break;
                    case TokenKind.Parameter:
                        color = PSStyle.Instance.Foreground.BrightBlack;
                        break;
                    case TokenKind.Number:
                        color = PSStyle.Instance.Foreground.BrightWhite;
                        break;
                    case TokenKind.Variable:
                    case TokenKind.SplattedVariable:
                        color = PSStyle.Instance.Foreground.BrightGreen;
                        break;
                    case TokenKind.StringExpandable:
                    case TokenKind.StringLiteral:
                    case TokenKind.HereStringExpandable:
                    case TokenKind.HereStringLiteral:
                        color = PSStyle.Instance.Foreground.Cyan;
                        break;
                    case TokenKind.Comment:
                        color = PSStyle.Instance.Foreground.Green;
                        break;
                    default:
                        color = PSStyle.Instance.Foreground.White;
                        break;
                }

                if (token.TokenFlags.HasFlag(TokenFlags.CommandName))
                {
                    color = PSStyle.Instance.Foreground.BrightYellow;
                }
                else if (token.TokenFlags.HasFlag(TokenFlags.Keyword))
                {
                    color = PSStyle.Instance.Foreground.BrightCyan;
                }
                else if (token.TokenFlags.HasFlag(TokenFlags.TypeName))
                {
                    color = PSStyle.Instance.Foreground.BrightBlue;
                }
                else if (token.TokenFlags.HasFlag(TokenFlags.MemberName))
                {
                    color = PSStyle.Instance.Foreground.White;
                }
                // check for all operators
                else if (token.Kind == TokenKind.Generic && token.Text.StartsWith("-"))
                {
                    color = PSStyle.Instance.Foreground.BrightBlack;
                }

                colorTokens.Add((token.Extent.StartOffset, color));
            }

            // walk backwards through the tokens and insert the color codes
            sb.Append(script);
            for (int i = colorTokens.Count - 1; i >= 0; i--)
            {
                var (start, color) = colorTokens[i];
                if (start < sb.Length)
                {
                    sb.Insert(start, color);
                }
            }

            return sb.ToString();
        }

        internal static string GetPrettyJson(string json)
        {
            using var jDoc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(jDoc, new JsonSerializerOptions { WriteIndented = true });
        }
    }
}
