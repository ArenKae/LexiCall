# API configuration, loaded from environment variables / .env.
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    api_key: str
    mongo_uri: str = "mongodb://localhost:27017"
    mongo_db_name: str = "lexicall"
    max_image_bytes: int = 2_000_000

    model_config = SettingsConfigDict(env_file=".env", case_sensitive=False)


settings = Settings()
