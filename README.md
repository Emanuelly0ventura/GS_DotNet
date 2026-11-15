# AgroConnect .NET | FIAP Global Solution 2026/2

O AgroConnect é um projeto CitizenTech voltado ao acesso de pequenos produtores a informações sobre suas plantações. O app reúne propriedades, talhões, medições, alertas e recomendações informativas para apoiar decisões. A demonstração usa dados simulados com identificação na resposta.

## Integrantes

Rafael Pascotte Mercadante
RM564928 - 2TDSPI

Rafael de Freitas Moraes
RM563210 - 2TDSPI

Carolina nascimento gonçalves
RM564786 - 2TDSPI

Emanuelly ventura do nascimento
RM562339 - 2TDSPI

Julia Sayuri Kina
RM564555 - 2TDSPI

## Rodar no Windows

Pré-requisito: SDK .NET 10. Na raiz da solução, execute:

```powershell
dotnet restore AgroConnect.sln
dotnet run --project AgroConnect.Api
```

Abra [Swagger](http://localhost:5000/swagger). O perfil inicia em Development na porta 5000 e cria o banco SQLite local automaticamente. Use `POST /api/v1/auth/register`, copie o `accessToken` da resposta e informe o token no botão **Authorize** do Swagger.

### Dados para a demonstração

Na primeira execução com dados de demonstração, defina no mesmo PowerShell:

```powershell
$env:Database__SeedDemoData = 'true'
$env:Demo__Password = Read-Host 'Senha da conta demo (8 a 128 caracteres)' -MaskInput
dotnet run --project AgroConnect.Api
```

Entre no app ou no Swagger como `demo@agroconnect.com` com a senha que você escolheu. A conta tem uma propriedade, dois dispositivos, leituras válidas e com falhas, um alerta e uma recomendação demonstrativa. O seed é executado somente em Development e cria a conta uma vez. Para atualizar os horários da demonstração sem apagar dados, configure `ConnectionStrings__Sqlite` com um novo arquivo, por exemplo `Data Source=agroconnect-demo-video.db;Foreign Keys=True`, e execute com o seed ativado.

### Conectar o app

Configure as duas URLs no arquivo `.env` da raiz do AgroConnect mobile. As duas podem apontar para o C# ao usar as recomendações demonstrativas.

| Ambiente | URL para as duas variáveis |
| --- | --- |
| Web no mesmo computador | `http://localhost:5000/api/v1` |
| Emulador Android | `http://10.0.2.2:5000/api/v1` |
| Celular na mesma rede | `http://IP_DO_COMPUTADOR:5000/api/v1` |
| Build de entrega | URL HTTPS pública da API com `/api/v1` |

```dotenv
EXPO_PUBLIC_API_URL=http://10.0.2.2:5000/api/v1
EXPO_PUBLIC_AI_API_URL=http://10.0.2.2:5000/api/v1
```

Para o celular ou o emulador, inicie a API com `dotnet run --project AgroConnect.Api -- --urls http://0.0.0.0:5000`. A porta deve estar acessível na rede de desenvolvimento. Reinicie o Expo depois de alterar o `.env`. O [guia de integração](docs/INTEGRACAO_MOBILE.md) lista as rotas e as etapas para demonstrá-las.

## PostgreSQL e publicação

Para testar com PostgreSQL em Docker, informe uma senha local e suba o serviço. O Compose publica o banco em `127.0.0.1:55432`.

```powershell
$env:POSTGRES_PASSWORD = [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(24))
docker compose up -d --wait postgres
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=55432;Database=agroconnect;Username=agroconnect;Password=$env:POSTGRES_PASSWORD"
$env:Database__ApplyMigrations = 'true'
dotnet run --project AgroConnect.Api
```

Se o volume do Compose já existir, use a senha com a qual ele foi criado. As migrations criam o schema `agroconnect` em um banco vazio. Em publicação, use PostgreSQL e forneça `ConnectionStrings__DefaultConnection` e uma chave JWT aleatória de pelo menos 32 bytes em `Jwt__Key` pelo gerenciador de segredos do ambiente. Em Development, sem uma chave explícita, a API cria uma chave efêmera e os tokens deixam de funcionar após reiniciar. Para Expo Web publicado, configure as origens permitidas em `Cors__Origins__0` etc.

## Itens da avaliação

| Item | Implementação |
| --- | --- |
| Minimal API e Swagger | Rotas em `/api/v1`, DTOs de entrada e saída, validações, erros em português e documentação interativa com JWT |
| JWT | Assinatura HS256, validação de emissor, audiência e expiração, autorização por proprietário e revogação da sessão no logout |
| Idempotência | Criações de propriedade, talhão, plantio e alerta gravam recurso e resposta em uma única transação. A mesma chave reproduz o `201` e o `Location`. Corpo diferente com a mesma chave retorna `409` |
| Rate limiting | 15 chamadas de login/cadastro por IP e 120 nas demais rotas por usuário autenticado ou IP, por janela de 60 segundos. O excesso retorna `429` com `Retry-After` |
| xUnit | Regras do domínio e testes de integração HTTP usando SQLite relacional e Expo simulado |
| Health checks | `/health` verifica que a API responde. `/health/ready` verifica o banco e os serviços externos configurados, retornando `503` em falha |

O mobile já cria propriedades sem cabeçalho de idempotência. Nesse caso, requisições com o mesmo usuário, rota e corpo são deduplicadas por cinco minutos. Clientes que precisem criar registros separados com os mesmos dados devem enviar chaves distintas. Atualizar uma propriedade com os mesmos valores preserva sua data de atualização, marcar alerta como lido preserva o estado e repetir um registro de push mantém um token por aparelho.

```powershell
dotnet test AgroConnect.sln
```

## Estrutura da solução

Os seis projetos ficam ao lado de `AgroConnect.sln`, seguindo a organização do PetCore.Net.

| Projeto | Conteúdo |
| --- | --- |
| `AgroConnect.Api` | Minimal API em `Endpoints`, JWT em `Authentication`, filtros, observabilidade e registro de serviços em `Extensions` |
| `AgroConnect.Application` | DTOs, interfaces de repositório, serviços de aplicação e segurança |
| `AgroConnect.Domain` | Entidades e regras de negócio |
| `AgroConnect.Infrastructure` | Repositórios EF Core, configurações em `Persistence/Configurations` e migrações em `Migrations` |
| `AgroConnect.Tests.Unit` | Regras de domínio |
| `AgroConnect.Tests.Integration` | Contrato mobile, saúde, segurança e push Expo |

Preencha `IDENTIFICACAO.txt` com nome e RM dos integrantes antes de entregar.
