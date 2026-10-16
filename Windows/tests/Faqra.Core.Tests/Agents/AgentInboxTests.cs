using Faqra.Core.Agents;

namespace Faqra.Core.Tests.Agents;

public class AgentInboxTests
{
    private const string Token = "0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData(@"\\.\pipe\LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c", @"LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c")]
    [InlineData(@"\\.\pipe\cc-msg-F482AC3A68B5E314E2A7BCADB825F61C", "cc-msg-F482AC3A68B5E314E2A7BCADB825F61C")]
    public void AcceptsClaudeCodesOwnInbox(string socket, string pipeName)
    {
        var inbox = AgentInbox.TryCreate(socket, Token)!;
        Assert.Equal((pipeName, Token), (inbox.PipeName, inbox.Token));
    }

    [Theory]
    [InlineData(@"\\.\pipe\faqra-agents-S-1-5-21")]
    [InlineData(@"\\server\pipe\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c")]
    [InlineData(@"\\.\pipe\LOCAL\..\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c")]
    [InlineData(@"\\.\pipe\LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61")]
    [InlineData(@"\\.\pipe\LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c\x")]
    [InlineData("/tmp/cc-socks-1000/cc-msg-f482ac3a68b5e314e2a7bcadb825f61c.sock")]
    [InlineData("\\\\.\\pipe\\LOCAL\\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c\n")]
    [InlineData("")]
    [InlineData(null)]
    public void RefusesAnythingButClaudeCodesInbox(string? socket) => Assert.Null(AgentInbox.TryCreate(socket, Token));

    [Theory]
    [InlineData("0123456789ABCDEF0123456789ABCDEF")]
    [InlineData("0123456789abcdef0123456789abcde")]
    [InlineData("0123456789abcdef0123456789abcdefa")]
    [InlineData("0123456789abcdef0123456789abcdeg")]
    [InlineData("0123456789abcdef0123456789abcdef\n")]
    [InlineData(null)]
    public void RefusesATokenThatIsNotClaudeCodes(string? token) =>
        Assert.Null(AgentInbox.TryCreate(@"\\.\pipe\LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c", token));

    [Fact]
    public void NeverShowsTheToken()
    {
        var inbox = AgentInbox.TryCreate(@"\\.\pipe\LOCAL\cc-msg-f482ac3a68b5e314e2a7bcadb825f61c", Token)!;
        Assert.DoesNotContain(Token, inbox.ToString());
    }
}
