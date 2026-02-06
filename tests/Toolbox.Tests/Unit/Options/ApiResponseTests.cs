using System.Net;
using System.Text.Json;
using Toolbox.Core.Options;

namespace Toolbox.Tests.Unit.Options;

/// <summary>
/// Unit tests for <see cref="ApiResponse"/>.
/// </summary>
public class ApiResponseTests
{
    #region Status Code Tests

    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.Created, true)]
    [InlineData(HttpStatusCode.NoContent, true)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public void IsSuccess_ShouldReturnCorrectValue(HttpStatusCode statusCode, bool expected)
    {
        // Arrange
        var response = new ApiResponse { StatusCode = statusCode };

        // Assert
        response.IsSuccess.Should().Be(expected);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, true)]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.NotFound, true)]
    [InlineData(HttpStatusCode.OK, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public void IsClientError_ShouldReturnCorrectValue(HttpStatusCode statusCode, bool expected)
    {
        // Arrange
        var response = new ApiResponse { StatusCode = statusCode };

        // Assert
        response.IsClientError.Should().Be(expected);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.BadGateway, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.OK, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    public void IsServerError_ShouldReturnCorrectValue(HttpStatusCode statusCode, bool expected)
    {
        // Arrange
        var response = new ApiResponse { StatusCode = statusCode };

        // Assert
        response.IsServerError.Should().Be(expected);
    }

    [Theory]
    [InlineData(HttpStatusCode.MovedPermanently, true)]
    [InlineData(HttpStatusCode.Redirect, true)]
    [InlineData(HttpStatusCode.TemporaryRedirect, true)]
    [InlineData(HttpStatusCode.OK, false)]
    public void IsRedirect_ShouldReturnCorrectValue(HttpStatusCode statusCode, bool expected)
    {
        // Arrange
        var response = new ApiResponse { StatusCode = statusCode };

        // Assert
        response.IsRedirect.Should().Be(expected);
    }

    [Fact]
    public void StatusCodeValue_ShouldReturnNumericValue()
    {
        // Arrange
        var response = new ApiResponse { StatusCode = HttpStatusCode.OK };

        // Assert
        response.StatusCodeValue.Should().Be(200);
    }

    #endregion

    #region Deserialize Tests

    [Fact]
    public void Deserialize_WithValidJson_ShouldReturnObject()
    {
        // Arrange
        var response = new ApiResponse
        {
            StatusCode = HttpStatusCode.OK,
            Body = """{"Name":"John","Age":30}"""
        };

        // Act
        var result = response.Deserialize<TestPerson>();

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("John");
        result.Age.Should().Be(30);
    }

    [Fact]
    public void Deserialize_WithEmptyBody_ShouldReturnDefault()
    {
        // Arrange
        var response = new ApiResponse
        {
            StatusCode = HttpStatusCode.OK,
            Body = null
        };

        // Act
        var result = response.Deserialize<TestPerson>();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_WithInvalidJson_ShouldThrowJsonException()
    {
        // Arrange
        var response = new ApiResponse
        {
            StatusCode = HttpStatusCode.OK,
            Body = "not valid json"
        };

        // Act
        var act = () => response.Deserialize<TestPerson>();

        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void TryDeserialize_WithValidJson_ShouldReturnTrue()
    {
        // Arrange
        var response = new ApiResponse
        {
            StatusCode = HttpStatusCode.OK,
            Body = """{"Name":"John","Age":30}"""
        };

        // Act
        var success = response.TryDeserialize<TestPerson>(out var result);

        // Assert
        success.Should().BeTrue();
        result.Should().NotBeNull();
    }

    [Fact]
    public void TryDeserialize_WithInvalidJson_ShouldReturnFalse()
    {
        // Arrange
        var response = new ApiResponse
        {
            StatusCode = HttpStatusCode.OK,
            Body = "not valid json"
        };

        // Act
        var success = response.TryDeserialize<TestPerson>(out var result);

        // Assert
        success.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryDeserialize_WithEmptyBody_ShouldReturnFalse()
    {
        // Arrange
        var response = new ApiResponse
        {
            StatusCode = HttpStatusCode.OK,
            Body = ""
        };

        // Act
        var success = response.TryDeserialize<TestPerson>(out var result);

        // Assert
        success.Should().BeFalse();
    }

    #endregion

    #region Header Tests

    [Fact]
    public void GetHeader_WithExistingHeader_ShouldReturnValue()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            ["Content-Type"] = new[] { "application/json" }
        };
        var response = new ApiResponse
        {
            StatusCode = HttpStatusCode.OK,
            Headers = headers
        };

        // Act
        var value = response.GetHeader("Content-Type");

        // Assert
        value.Should().Be("application/json");
    }

    [Fact]
    public void GetHeader_WithNonExistingHeader_ShouldReturnNull()
    {
        // Arrange
        var response = new ApiResponse
        {
            StatusCode = HttpStatusCode.OK,
            Headers = new Dictionary<string, IEnumerable<string>>()
        };

        // Act
        var value = response.GetHeader("X-Custom");

        // Assert
        value.Should().BeNull();
    }

    [Fact]
    public void GetHeaders_WithMultipleValues_ShouldReturnAll()
    {
        // Arrange
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            ["Set-Cookie"] = new[] { "cookie1=value1", "cookie2=value2" }
        };
        var response = new ApiResponse
        {
            StatusCode = HttpStatusCode.OK,
            Headers = headers
        };

        // Act
        var values = response.GetHeaders("Set-Cookie").ToList();

        // Assert
        values.Should().HaveCount(2);
    }

    #endregion

    #region EnsureSuccess Tests

    [Fact]
    public void EnsureSuccess_WithSuccessStatus_ShouldNotThrow()
    {
        // Arrange
        var response = new ApiResponse { StatusCode = HttpStatusCode.OK };

        // Act
        var act = () => response.EnsureSuccess();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void EnsureSuccess_WithErrorStatus_ShouldThrowHttpRequestException()
    {
        // Arrange
        var response = new ApiResponse
        {
            StatusCode = HttpStatusCode.BadRequest,
            ReasonPhrase = "Bad Request",
            Body = "Invalid input"
        };

        // Act
        var act = () => response.EnsureSuccess();

        // Assert
        act.Should().Throw<HttpRequestException>()
            .WithMessage("*400*");
    }

    #endregion

    #region Factory Methods Tests

    [Fact]
    public void Success_ShouldCreateSuccessResponse()
    {
        // Act
        var response = ApiResponse.Success("OK", HttpStatusCode.Created);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Body.Should().Be("OK");
        response.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Error_ShouldCreateErrorResponse()
    {
        // Act
        var response = ApiResponse.Error(HttpStatusCode.NotFound, "Not found");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Body.Should().Be("Not found");
        response.IsSuccess.Should().BeFalse();
    }

    #endregion

    // Helper class for deserialization tests
    private class TestPerson
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }
}
