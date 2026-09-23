from dataclasses import dataclass


@dataclass(frozen=True, slots=True)
class RateLimitStatus:
    short_term_usage: int
    short_term_limit: int
    daily_usage: int
    daily_limit: int

    @classmethod
    def from_headers(cls, headers): raise NotImplementedError
