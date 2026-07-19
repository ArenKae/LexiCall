# Production image for the API. No --reload: hot reload is reserved for
# local dev (see justfile / docker-compose.yml).
FROM python:3.12-slim

WORKDIR /app

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY src/ src/

CMD ["uvicorn", "lexicall_api.main:app", "--host", "0.0.0.0", "--port", "8000", "--app-dir", "src"]
