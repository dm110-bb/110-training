using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using OrderHub.Core.Ai;
using OrderHub.Core.Domain;
using OrderHub.Core.Services;

namespace OrderHub.Web.Controllers.Api;

[ApiController]
[Route("api/orders")]
public class OrdersApiController : ControllerBase
{
    private readonly IOrderSearchService _orderSearchService;
    private readonly IOrderService _orderService;

    public OrdersApiController(IOrderSearchService orderSearchService, IOrderService orderService)
    {
        _orderSearchService = orderSearchService;
        _orderService = orderService;
    }

    [HttpPost("search")]
    [ProducesResponseType<IReadOnlyList<OrderSearchResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Search(
        [FromBody] OrderSearchRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _orderSearchService.SearchAsync(request.Text, cancellationToken);
            if (!result.Success)
                return UnprocessableEntity(new { error = result.ErrorMessage });

            var response = result.Value!.Select(order => new OrderSearchResponse(
                order.Id,
                order.Customer?.Name ?? "-",
                order.Customer?.Tier ?? CustomerTier.Standard,
                order.Status,
                _orderService.CalculateTotal(order),
                order.CreatedAt));

            return Ok(response);
        }
        catch (AiServiceUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }
}

public sealed record OrderSearchRequest([param: Required] string Text);

public sealed record OrderSearchResponse(
    int Id,
    string CustomerName,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] CustomerTier CustomerTier,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] OrderStatus Status,
    decimal Total,
    DateTime CreatedAt);
