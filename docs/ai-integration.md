# AI Integration

Green City Reporter uses a single `IAIService` abstraction and selects the active provider from configuration. Local development defaults to Ollama, while production uses Groq Cloud when `AI__Provider=Groq` is set.

## Supported providers

- Local development: Ollama via `OllamaAIService`
- Production: Groq via `GroqAIService`

The default local endpoint is `http://localhost:11434`, and the default local model is `llama3.2`.

## Inputs and outputs

Report title and description are sent to the active provider for:

- category selection from the categories currently stored in the database;
- priority detection as `Low`, `Medium`, `High`, or `Critical`;
- a short administrative summary;
- criticality detection during classification.

The chatbot sends the user's message plus server-selected context. User-specific report context is limited to reports owned by the authenticated user.

Category output is matched case-insensitively against the supplied category list. Structured JSON classification output is parsed rigorously, confidence is clamped to the range 0 to 1, and invalid or invented categories are rejected.

## Fallback behavior

AI is an enhancement, not a submission requirement. Timeouts, invalid responses, unavailable models, HTTP 429/401/403/5xx responses, malformed JSON, and caller cancellation return null results. The report workflow then allows manual category selection, uses the normal priority fallback, and continues without an AI summary. Failures are logged without exposing API keys or credentials.

## Configuration

Use these settings:

- `AI__Provider=Ollama` for local development
- `AI__Provider=Groq` for Render/production
- `AI__Ollama__BaseUrl=http://localhost:11434`
- `AI__Ollama__Model=llama3.2`
- `AI__Groq__BaseUrl=https://api.groq.com/openai/v1`
- `AI__Groq__Model=<groq-model>`
- `AI__Groq__ApiKey=<secret>`
- `AI__TimeoutSeconds=30`

Never commit the Groq API key. Keep production secrets in Render environment variables or a secure secret manager.

## Deployment considerations

A deployment must provide a reachable AI service for the selected provider, or accept the documented fallback behavior. Do not send passwords, payment credentials, authentication tokens, or unrestricted database contents to the model.
