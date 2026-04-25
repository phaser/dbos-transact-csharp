using System.Text.Json;
using Dbos.Transact.Conductor.Protocol;

namespace Dbos.Transact.Tests.Conductor.Protocol;

public class BaseMessageTests
{
    [Fact]
    public void AlertRequest_Type_IsSetInDefaultConstructor()
    {
        var msg = new AlertRequest();
        Assert.Equal("alert", msg.Type);
    }

    [Fact]
    public void AlertRequest_Serializes_WithTypeDiscriminator()
    {
        var msg = new AlertRequest("req-1", "test-alert", "something happened", null);
        var json = JsonSerializer.Serialize<BaseMessage>(msg);
        Assert.Contains("\"type\":\"alert\"", json);
        Assert.Contains("\"request_id\":\"req-1\"", json);
    }

    [Fact]
    public void Deserialize_AlertJson_ProducesAlertRequest()
    {
        var json = """{"type":"alert","request_id":"r-42","name":"my-alert","message":"msg"}""";
        var msg = JsonSerializer.Deserialize<BaseMessage>(json);
        Assert.IsType<AlertRequest>(msg);
        var alert = (AlertRequest)msg!;
        Assert.Equal("r-42", alert.RequestId);
        Assert.Equal("my-alert", alert.Name);
        Assert.Equal("msg", alert.Message);
    }

    [Fact]
    public void Deserialize_UnknownTypeDiscriminator_ThrowsNotSupportedException()
    {
        // IgnoreUnrecognizedTypeDiscriminators=true skips the discriminator error but STJ still
        // can't instantiate the abstract base type, so deserialization throws.
        var json = """{"type":"unknown_type_xyz","request_id":"r-1"}""";
        Assert.Throws<NotSupportedException>(() => JsonSerializer.Deserialize<BaseMessage>(json));
    }

    [Fact]
    public void CancelRequest_Type_IsCancel()
    {
        var msg = new CancelRequest();
        Assert.Equal("cancel", msg.Type);
    }

    [Fact]
    public void ListWorkflowsRequest_Type_IsListWorkflows()
    {
        var msg = new ListWorkflowsRequest();
        Assert.Equal("list_workflows", msg.Type);
    }

    [Fact]
    public void SuccessResponse_ErrorMessage_SetOnException()
    {
        var req = new AlertRequest("req-1", "n", "m", null);
        var ex = new InvalidOperationException("something went wrong");
        var response = new SuccessResponse(req, ex);
        Assert.Equal("something went wrong", response.ErrorMessage);
    }

    [Fact]
    public void SuccessResponse_SuccessConstructor_SetsType()
    {
        var req = new AlertRequest("req-1", "n", "m", null);
        var response = new SuccessResponse(req, true);
        Assert.Equal("alert", response.Type);
        Assert.Equal("req-1", response.RequestId);
        Assert.True(response.Success);
    }
}
