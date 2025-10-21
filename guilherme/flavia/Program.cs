using flavia;
using flavia.Model;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString)
);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

app.UseSwagger();
app.UseSwaggerUI();


double CalcularConsumo(double m3Consumidos)
{
    return m3Consumidos < 10 ? 10 : m3Consumidos;
}

double CalcularTarifa(double FaturaDoConsumo)
{
    if (FaturaDoConsumo <= 10) return 2.50;
    if (FaturaDoConsumo <= 20) return 3.50;
    if (FaturaDoConsumo <= 50) return 5.00;
    return 6.50;
}

double CalcularAgua(double FaturaDoConsumo, double tarifa)
{
    return FaturaDoConsumo * tarifa;
}

double CalcularBandeira(double valorAgua, string bandeira)
{
    return bandeira.ToUpper() switch
    {
        "VERDE" => 0,
        "AMARELA" => valorAgua * 0.10,
        "VERMELHA" => valorAgua * 0.20,
        _ => 0
    };
}

double CalcularTaxaEsgoto(double valorAgua, double AdicionalDaBandeira, bool possuiEsgoto)
{
    if (!possuiEsgoto) return 0;
    return (valorAgua + AdicionalDaBandeira) * 0.80;
}

double CalcularTotal(double valorAgua, double AdicionalDaBandeira, double taxaEsgoto)
{
    return valorAgua + AdicionalDaBandeira + taxaEsgoto;
}


app.MapPost("/api/consumo/cadastrar", async (ConsumoRequest request, AppDbContext db) =>
{

    if (request.Mes < 1 || request.Mes > 12)
        return Results.BadRequest("Digite um mes entre 1 e 12");
    
    if (request.Ano < 2000)
        return Results.BadRequest("Ano deve ser maior ou igual a 2000");
    
    if (request.M3Consumidos <= 0)
        return Results.BadRequest("Consumo deve ser maior que zero");
    
    var bandeiraCor = request.Bandeira.ToUpper();
    if (bandeiraCor != "VERDE" && bandeiraCor != "AMARELA" && bandeiraCor != "VERMELHA")
        return Results.BadRequest("Bandeira deve ser VERDE AMARELA ou VERMELHA");

    var jaExiste = await db.ConsumosAgua
        .AnyAsync(c => c.Cpf == request.Cpf && c.Mes == request.Mes && c.Ano == request.Ano);
    
    if (jaExiste)
        return Results.BadRequest("Já existe cadastro para esse Cpf mes e ano");

    var FaturaDoConsumo = CalcularConsumo(request.M3Consumidos);
    var tarifa = CalcularTarifa(FaturaDoConsumo);
    var valorAgua = CalcularAgua(FaturaDoConsumo, tarifa);
    var AdicionalDaBandeira = CalcularBandeira(valorAgua, request.Bandeira);
    var taxaEsgoto = CalcularTaxaEsgoto(valorAgua, AdicionalDaBandeira, request.PossuiEsgoto);
    var total = CalcularTotal(valorAgua, AdicionalDaBandeira, taxaEsgoto);

    var consumo = new ConsumoAgua
    {
        Cpf = request.Cpf,
        Mes = request.Mes,
        Ano = request.Ano,
        M3Consumidos = request.M3Consumidos,
        Bandeira = request.Bandeira,
        PossuiEsgoto = request.PossuiEsgoto,
        FaturaDoConsumo = Math.Round(FaturaDoConsumo, 2),
        Tarifa = Math.Round(tarifa, 2),
        ValorAgua = Math.Round(valorAgua, 2),
        AdicionalDaBandeira = Math.Round(AdicionalDaBandeira, 2),
        TaxaEsgoto = Math.Round(taxaEsgoto, 2),
        Total = Math.Round(total, 2)
    };

    db.ConsumosAgua.Add(consumo);
    await db.SaveChangesAsync();

    return Results.Created($"/api/consumo/{consumo.Id}", consumo);
});


app.MapGet("/api/consumo/listar", async (AppDbContext db) =>
{
    var consumos = await db.ConsumosAgua.ToListAsync();
    return Results.Ok(consumos);
});


app.MapGet("/api/consumo/buscar/{cpf}/{mes}/{ano}", async (string cpf, int mes, int ano, AppDbContext db) =>
{
    var consumo = await db.ConsumosAgua
        .FirstOrDefaultAsync(c => c.Cpf == cpf && c.Mes == mes && c.Ano == ano);
    
    if (consumo == null)
        return Results.NotFound("Consumo não foi encontrado");
    
    return Results.Ok(consumo);
});


app.MapDelete("/api/consumo/remover/{cpf}/{mes}/{ano}", async (string cpf, int mes, int ano, AppDbContext db) =>
{
    var consumo = await db.ConsumosAgua
        .FirstOrDefaultAsync(c => c.Cpf == cpf && c.Mes == mes && c.Ano == ano);
    
    if (consumo == null)
        return Results.NotFound("Consumo não foi encontrado");
    
    db.ConsumosAgua.Remove(consumo);
    await db.SaveChangesAsync();
    
    return Results.Ok();
});


app.MapGet("/api/consumo/total-geral", async (AppDbContext db) =>
{
    var consumos = await db.ConsumosAgua.ToListAsync();
    
    var totalGeral = consumos.Sum(c => c.Total);
    
    return Results.Ok(new { totalGeral = Math.Round(totalGeral, 2) });
});

app.Run();
//////////////////////////////////////////////
public class ConsumoRequest
{
    public string Cpf { get; set; } = string.Empty;
    public int Mes { get; set; }
    public int Ano { get; set; }
    public double M3Consumidos { get; set; }
    public string Bandeira { get; set; } = string.Empty;
    public bool PossuiEsgoto { get; set; }
}