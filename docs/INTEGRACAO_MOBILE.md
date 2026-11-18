# Integração do AgroConnect mobile

O backend usa o contrato documentado em `AgroConnect/docs/API_CONTRACT.md` e os formatos efetivamente consumidos por `AgroConnect/src/services`. Os IDs são UUID, datas JSON são UTC com sufixo `Z` e listas têm `items` e `page`. As listas aceitam `page` e `size`, com padrão 1 e 20 e tamanho máximo 100. Use `Authorization: Bearer <accessToken>`.

| Serviço no mobile | Método e rota | Regras e formato conferidos |
| --- | --- | --- |
| `authService.ts` | `POST /auth/login` | E-mail e senha. Retorna JWT e usuário em `200` |
| `authService.ts` | `POST /auth/register` | Nome, e-mail e senha de 8 a 128 caracteres. Retorna a sessão em `201` e trata e-mail repetido com `409` |
| `authService.ts` | `GET /auth/me` | Retorna o usuário da sessão |
| `authService.ts` | `POST /auth/logout` | Usa o Bearer e dispensa corpo. Encerra a sessão em `204` |
| `propertyService.ts` | `GET /properties` | Retorna a lista paginada do usuário |
| `propertyService.ts` | `POST /properties` | Valida nome, cidade, UF de duas letras e área positiva. Retorna `201` e aceita o envio do app sem `Idempotency-Key` |
| `propertyService.ts` | `GET /properties/{id}` | Retorna a propriedade do usuário |
| `propertyService.ts` | `PATCH /properties/{id}` | Aceita todos os campos enviados pelo app e atualizações parciais. `description: null` limpa a descrição |
| `propertyService.ts` | `DELETE /properties/{id}` | Exclui a propriedade e retorna `204` |
| `deviceService.ts` | `GET /devices` | Retorna dispositivos paginados com status calculado pela última comunicação |
| `deviceService.ts` | `GET /devices/{id}` | Retorna os detalhes do dispositivo |
| `deviceService.ts` | `GET /devices/{id}/sensors` | Retorna sensores paginados |
| `deviceService.ts` | `GET /devices/{id}/readings/latest` | Retorna a última leitura disponível de cada sensor, com qualidade e data |
| `historyService.ts` | `GET /devices/{id}/readings` | Aceita `sensorId`, `from`, `to` e `interval` como enviados pelo app |
| `alertService.ts` | `GET /alerts` | Lista paginada com filtro opcional de status e ordenação por severidade e data |
| `alertService.ts` | `PATCH /alerts/{id}/read` | Aceita chamada sem corpo. Altera `UNREAD` para `READ` e retorna o alerta |
| `notificationService.ts` | `POST /me/push-tokens` | Aceita `token` e `platform` e faz registro idempotente com `204` |
| `recommendationService.ts` | `POST /recommendations` | Aceita `deviceId` e responde com `Page<Recommendation>`. O app usa `EXPO_PUBLIC_AI_API_URL` para esta rota |

A API também oferece rotas complementares para talhões, plantios e criação de alertas.

Rotas da tabela têm prefixo `/api/v1`. Recursos pertencentes a outro usuário retornam `404`. O histórico aceita `sensorId` ou `type`, `from`, `to` e `interval` (`raw`, `5m`, `15m`, `1h`, `1d`). Intervalos vazios aparecem como lacunas. Leituras com falhas têm `value: null` e o código de qualidade correspondente.

## Roteiro de demonstração

1. Entre como `demo@agroconnect.com` com a senha configurada. Confirme que o perfil carrega.
2. Na aba de plantação, crie uma propriedade, edite os dados e exclua a propriedade criada.
3. Em sensores, abra `ESP32-001` e `ESP32-002`. Consulte as leituras e o histórico de um sensor. O segundo dispositivo demonstra comunicação antiga e falha do sensor.
4. Abra um alerta, marque-o como lido e solicite recomendações para o dispositivo do alerta.
5. Saia da conta. O backend revoga o JWT dessa sessão e exclui o registro de push vinculado a ela.

## Recomendações e dados para a equipe de IA

A rota demonstrativa do C# responde ao contrato que o app usa, com `reason`, `basedOn`, `disclaimer` e `simulatedData: true`. Ela usa os alertas com evidências do dispositivo e respeita a autorização do usuário. Ao conectar outro serviço, aponte `EXPO_PUBLIC_AI_API_URL` para uma API que implemente `POST /api/v1/recommendations` com o mesmo contrato e aceite o JWT. Configure `Services__AiHealthUrl` para a URL completa de saúde externa se também quiser monitorá-la pelo C#.

O JWT do C# usa `HS256`, issuer `AgroConnect`, audience `AgroConnect.Mobile`, `sub` para UUID do usuário, `jti` para UUID da sessão e `exp` com validade de uma hora. A revogação é consultada em `agroconnect."Sessions"`. Prefira consultar uma interface autenticada do backend para autorização ao integrar outro serviço. O PostgreSQL mantém relações de propriedades, talhões e dispositivos nas tabelas do schema `agroconnect`. As migrations no projeto de infraestrutura registram os nomes físicos e tipos.

## Push via Expo

Para testar com o APK, habilite `Push__Enabled=true` no backend e configure as credenciais de push no projeto EAS. Se o serviço Expo usar segurança de push, forneça `Push__AccessToken` fora do repositório. O app registra o token após login quando o usuário concede permissão.

No Swagger, autenticado com o mesmo usuário do app, crie um novo alerta via `POST /api/v1/alertas`, informando `deviceId`, `severity`, `title`, `message` e `reason`. O backend consulta as leituras válidas disponíveis, grava o alerta e o coloca na fila quando o push está habilitado. A notificação abre a lista de alertas. O alerta inicial do seed serve para mostrar a interface e não entra nessa fila.

O teste de integração substitui o endpoint de envio por um serviço simulado. O backend envia uma vez por alerta e aparelho e trata o retorno imediato do Expo. A implementação não consulta recibos posteriores, e um timeout pode impedir a confirmação de entrega. Detalhes de credenciais e recibos estão na [documentação oficial da Expo](https://docs.expo.dev/push-notifications/sending-notifications/).
