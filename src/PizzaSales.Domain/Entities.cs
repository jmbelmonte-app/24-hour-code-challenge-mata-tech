namespace PizzaSales.Domain;

public sealed class PizzaType
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Ingredients { get; init; }
    public ICollection<Pizza> Pizzas { get; } = new List<Pizza>();
}

public sealed class Pizza
{
    public required string Id { get; init; }
    public required string PizzaTypeId { get; init; }
    public required string Size { get; init; }
    public int CurrentPriceCents { get; init; }
    public PizzaType PizzaType { get; init; } = null!;
    public ICollection<OrderItem> OrderItems { get; } = new List<OrderItem>();
}

public sealed class Order
{
    public int Id { get; init; }
    public DateOnly OrderDate { get; init; }
    public TimeOnly OrderTime { get; init; }
    public ICollection<OrderItem> Items { get; } = new List<OrderItem>();
}

public sealed class OrderItem
{
    public int Id { get; init; }
    public int OrderId { get; init; }
    public required string PizzaId { get; init; }
    public int Quantity { get; init; }
    public int UnitPriceCents { get; init; }
    public Order Order { get; init; } = null!;
    public Pizza Pizza { get; init; } = null!;
}
