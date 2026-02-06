using Toolbox.Core.Options;

namespace Toolbox.Tests.Unit.Options;

/// <summary>
/// Unit tests for <see cref="ApiRequest"/> and related classes.
/// </summary>
public class ApiRequestTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreate()
    {
        // Arrange & Act
        var request = new ApiRequest("GET", "/users");

        // Assert
        request.Method.Should().Be("GET");
        request.Url.Should().Be("/users");
    }

    [Fact]
    public void Constructor_ShouldUppercaseMethod()
    {
        // Arrange & Act
        var request = new ApiRequest("get", "/users");

        // Assert
        request.Method.Should().Be("GET");
    }

    [Fact]
    public void Constructor_WithNullMethod_ShouldThrow()
    {
        // Act
        var act = () => new ApiRequest(null!, "/users");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullUrl_ShouldThrow()
    {
        // Act
        var act = () => new ApiRequest("GET", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Factory Methods Tests

    [Fact]
    public void Get_ShouldCreateGetRequest()
    {
        // Act
        var request = ApiRequest.Get("/users");

        // Assert
        request.Method.Should().Be("GET");
        request.Url.Should().Be("/users");
    }

    [Fact]
    public void Post_ShouldCreatePostRequest()
    {
        // Act
        var request = ApiRequest.Post("/users");

        // Assert
        request.Method.Should().Be("POST");
        request.Url.Should().Be("/users");
    }

    [Fact]
    public void Post_WithBody_ShouldSetJsonContent()
    {
        // Arrange
        var body = new { Name = "John", Age = 30 };

        // Act
        var request = ApiRequest.Post("/users", body);

        // Assert
        request.Method.Should().Be("POST");
        request.Content.Should().NotBeNull();
    }

    [Fact]
    public void Put_ShouldCreatePutRequest()
    {
        // Act
        var request = ApiRequest.Put("/users/1");

        // Assert
        request.Method.Should().Be("PUT");
    }

    [Fact]
    public void Patch_ShouldCreatePatchRequest()
    {
        // Act
        var request = ApiRequest.Patch("/users/1");

        // Assert
        request.Method.Should().Be("PATCH");
    }

    [Fact]
    public void Delete_ShouldCreateDeleteRequest()
    {
        // Act
        var request = ApiRequest.Delete("/users/1");

        // Assert
        request.Method.Should().Be("DELETE");
    }

    [Fact]
    public void Head_ShouldCreateHeadRequest()
    {
        // Act
        var request = ApiRequest.Head("/users");

        // Assert
        request.Method.Should().Be("HEAD");
    }

    [Fact]
    public void Options_ShouldCreateOptionsRequest()
    {
        // Act
        var request = ApiRequest.Options("/users");

        // Assert
        request.Method.Should().Be("OPTIONS");
    }

    #endregion

    #region Fluent Methods Tests

    [Fact]
    public void WithHeader_ShouldAddHeader()
    {
        // Arrange
        var request = ApiRequest.Get("/users");

        // Act
        request.WithHeader("X-Custom", "value");

        // Assert
        request.Headers.Should().ContainKey("X-Custom");
        request.Headers["X-Custom"].Should().Be("value");
    }

    [Fact]
    public void WithHeader_ShouldSupportChaining()
    {
        // Act
        var request = ApiRequest.Get("/users")
            .WithHeader("X-First", "1")
            .WithHeader("X-Second", "2");

        // Assert
        request.Headers.Should().HaveCount(2);
    }

    [Fact]
    public void WithQuery_ShouldAddQueryParameter()
    {
        // Arrange
        var request = ApiRequest.Get("/users");

        // Act
        request.WithQuery("page", "1");

        // Assert
        request.QueryParameters.Should().ContainKey("page");
        request.QueryParameters["page"].Should().Be("1");
    }

    [Fact]
    public void WithTimeout_ShouldSetTimeout()
    {
        // Arrange
        var request = ApiRequest.Get("/users");

        // Act
        request.WithTimeout(TimeSpan.FromMinutes(5));

        // Assert
        request.Timeout.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void SetJsonContent_ShouldSetContent()
    {
        // Arrange
        var request = ApiRequest.Post("/users");
        var body = new { Name = "John" };

        // Act
        request.SetJsonContent(body);

        // Assert
        request.Content.Should().NotBeNull();
    }

    [Fact]
    public void SetStringContent_ShouldSetContent()
    {
        // Arrange
        var request = ApiRequest.Post("/users");

        // Act
        request.SetStringContent("<xml/>", "application/xml");

        // Assert
        request.Content.Should().NotBeNull();
    }

    [Fact]
    public void SetFormContent_ShouldSetContent()
    {
        // Arrange
        var request = ApiRequest.Post("/login");
        var formData = new Dictionary<string, string>
        {
            ["username"] = "user",
            ["password"] = "pass"
        };

        // Act
        request.SetFormContent(formData);

        // Assert
        request.Content.Should().NotBeNull();
    }

    [Fact]
    public void SetBinaryContent_ShouldSetContent()
    {
        // Arrange
        var request = ApiRequest.Post("/upload");
        var bytes = new byte[] { 1, 2, 3 };

        // Act
        request.SetBinaryContent(bytes);

        // Assert
        request.Content.Should().NotBeNull();
    }

    #endregion

    #region BuildUrl Tests

    [Fact]
    public void BuildUrl_WithNoQueryParams_ShouldReturnUrl()
    {
        // Arrange
        var request = ApiRequest.Get("/users");

        // Act
        var url = request.BuildUrl();

        // Assert
        url.Should().Be("/users");
    }

    [Fact]
    public void BuildUrl_WithQueryParams_ShouldAppendQueryString()
    {
        // Arrange
        var request = ApiRequest.Get("/users")
            .WithQuery("page", "1")
            .WithQuery("limit", "10");

        // Act
        var url = request.BuildUrl();

        // Assert
        url.Should().Contain("page=1");
        url.Should().Contain("limit=10");
        url.Should().StartWith("/users?");
    }

    [Fact]
    public void BuildUrl_WithExistingQueryString_ShouldAppendWithAmpersand()
    {
        // Arrange
        var request = ApiRequest.Get("/users?active=true")
            .WithQuery("page", "1");

        // Act
        var url = request.BuildUrl();

        // Assert
        url.Should().StartWith("/users?active=true&");
        url.Should().Contain("page=1");
    }

    [Fact]
    public void BuildUrl_ShouldEncodeSpecialCharacters()
    {
        // Arrange
        var request = ApiRequest.Get("/search")
            .WithQuery("q", "hello world");

        // Act
        var url = request.BuildUrl();

        // Assert
        url.Should().Contain("hello%20world");
    }

    #endregion
}
