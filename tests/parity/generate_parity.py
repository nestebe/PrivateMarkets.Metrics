"""Generate the parity datasets used by PrivateMarkets.Metrics.Tests.

Each dataset holds synthetic but realistic fund cash flows, NAVs and benchmark index levels,
together with the values computed by the Python reference implementations:

* xirr.json    -> `xirr` package (Excel XIRR, ACT/365) and `privateassets` (ACT/365.25)
* pme.json     -> `privateassets` 0.7.1: KS-PME, Direct Alpha, Long-Nickels ratio / shadow NAV
                  plus an independent SciPy reference for PME+ and the Long-Nickels IRR
* mpme.json    -> `pypme` 0.7.0: modified PME (mPME) IRR and asset IRR
* vintage.json -> `privateassets` vintage statistics, NumPy quartiles, SciPy pooled IRR

Usage (from the repository root):
    python -m venv .venv && .venv/Scripts/pip install -r tests/parity/requirements.txt
    .venv/Scripts/python -I tests/parity/generate_parity.py

The random generator is seeded, so the output is deterministic for pinned package versions.
"""

from __future__ import annotations

import datetime as dt
import json
import math
from pathlib import Path

import numpy as np
import pandas as pd
from scipy.optimize import brentq
from xirr.math import listsXirr
from pypme import verbose_xpme
from privateassets.matf._pme import (
    compute_vintage_stats,
    direct_alpha,
    ks_pme,
    long_nickels_pme,
    xirr as privateassets_xirr,
)

OUTPUT_DIR = Path(__file__).parent
RNG = np.random.default_rng(20261103)


# ---------------------------------------------------------------------------
# helpers
# ---------------------------------------------------------------------------

def iso(day: dt.date) -> str:
    return day.isoformat()


def as_date(ts) -> dt.date:
    return pd.Timestamp(ts).date()


def reference_xirr(dates: list[dt.date], amounts: list[float], days_per_year: float) -> float | None:
    """Independent XIRR: scan for the first sign change, then Brent to machine precision."""
    origin = min(dates)
    years = np.array([(d - origin).days / days_per_year for d in dates])
    values = np.array(amounts, dtype=float)

    def npv(rate: float) -> float:
        return float(np.sum(values / (1.0 + rate) ** years))

    grid = np.concatenate([np.linspace(-0.99, 1.0, 400), np.geomspace(1.0, 1e4, 80)[1:]])
    npvs = [npv(r) for r in grid]
    for i in range(len(grid) - 1):
        if np.sign(npvs[i]) != np.sign(npvs[i + 1]):
            return brentq(npv, grid[i], grid[i + 1], xtol=1e-15, rtol=1e-15, maxiter=1000)
    return None


def random_business_day(start: dt.date, max_offset: int) -> dt.date:
    day = start + dt.timedelta(days=int(RNG.integers(0, max_offset)))
    while day.weekday() >= 5:
        day += dt.timedelta(days=1)
    return day


def gbm_index(start: dt.date, end: dt.date, drift: float, volatility: float, level: float = 100.0) -> pd.Series:
    """Daily (business days) total-return index following a geometric Brownian motion."""
    days = pd.bdate_range(start, end)
    dt_year = 1.0 / 252.0
    shocks = RNG.normal((drift - 0.5 * volatility ** 2) * dt_year, volatility * math.sqrt(dt_year), len(days))
    shocks[0] = 0.0
    levels = np.round(level * np.exp(np.cumsum(shocks)), 4)
    return pd.Series(levels, index=days)


def index_points(series: pd.Series) -> list[dict]:
    return [{"date": iso(as_date(d)), "level": float(v)} for d, v in series.items()]


def synthetic_fund(first_day: dt.date, commitment: float, quality: float, years: int = 10) -> list[dict]:
    """Calls over the investment period, distributions afterwards; a few fees and recallable distributions."""
    flows: list[dict] = []
    called = 0.0
    for quarter in range(years * 4):
        quarter_start = (pd.Timestamp(first_day) + pd.DateOffset(months=3 * quarter)).date()
        day = random_business_day(quarter_start, 80)
        if quarter == 0 or (quarter < 16 and RNG.random() < 0.6 and called < commitment * 0.95):
            amount = round(min(commitment * RNG.uniform(0.04, 0.14), commitment - called), 2)
            if amount > 0:
                called += amount
                kind = "Fee" if RNG.random() < 0.15 and quarter > 0 else "Call"
                flows.append({"date": iso(day), "amount": amount, "type": kind})
        elif quarter >= 8 and RNG.random() < 0.55:
            amount = round(commitment * RNG.uniform(0.02, 0.12) * quality, 2)
            kind = "RecallableDistribution" if RNG.random() < 0.1 else "Distribution"
            flows.append({"date": iso(day), "amount": amount, "type": kind})
    return flows


def signed(flow: dict) -> float:
    return -flow["amount"] if flow["type"] in ("Call", "Fee") else flow["amount"]


# ---------------------------------------------------------------------------
# datasets
# ---------------------------------------------------------------------------

def xirr_dataset() -> dict:
    cases = [{
        "name": "excel-documentation-example",
        "flows": [
            {"date": "2008-01-01", "amount": -10000.0},
            {"date": "2008-03-01", "amount": 2750.0},
            {"date": "2008-10-30", "amount": 4250.0},
            {"date": "2009-02-15", "amount": 3250.0},
            {"date": "2009-04-01", "amount": 2750.0},
        ],
    }]
    profiles = [("loss", 0.4), ("flat", 1.0), ("moderate", 1.6), ("strong", 2.5), ("home-run", 6.0)]
    for name, multiple in profiles:
        for variant in range(3):
            start = random_business_day(dt.date(2005 + variant * 3, 1, 1), 300)
            flows = []
            invested = 0.0
            for k in range(int(RNG.integers(2, 6))):
                amount = round(RNG.uniform(1e5, 5e6), 2)
                invested += amount
                flows.append({"date": iso(start + dt.timedelta(days=150 * k + int(RNG.integers(0, 60)))), "amount": -amount})
            exits = int(RNG.integers(1, 5))
            for k in range(exits):
                day = start + dt.timedelta(days=900 + 300 * k + int(RNG.integers(0, 200)))
                flows.append({"date": iso(day), "amount": round(invested * multiple / exits, 2)})
            cases.append({"name": f"{name}-{variant}", "flows": flows})

    for case in cases:
        dates = [dt.date.fromisoformat(f["date"]) for f in case["flows"]]
        amounts = [f["amount"] for f in case["flows"]]
        case["expected"] = {
            "Actual365Fixed": listsXirr(dates, amounts),
            "Actual36525": privateassets_xirr(pd.Series(pd.to_datetime(dates)), pd.Series(amounts), lo=-0.99, hi=50.0),
            "Actual360": reference_xirr(dates, amounts, 360.0),
        }
    return {
        "source": "xirr==0.1.8 (ACT/365), privateassets==0.7.1 xirr (ACT/365.25), scipy brentq (ACT/360)",
        "cases": cases,
    }


def pme_dataset() -> dict:
    scenarios = [
        ("buyout-outperforming", 2.0, 0.07, 0.16),
        ("buyout-underperforming", 0.9, 0.09, 0.15),
        ("venture-volatile", 1.6, 0.06, 0.25),
        ("fast-distributor-shadow-negative", 4.0, -0.02, 0.12),
        ("young-fund-no-distribution", 0.0, 0.08, 0.15),
        ("mature-fund", 1.4, 0.05, 0.18),
    ]
    cases = []
    for name, quality, drift, volatility in scenarios:
        first_day = random_business_day(dt.date(2010, 1, 1), 200)
        years = 4 if quality == 0.0 else 10
        flows = synthetic_fund(first_day, 50_000_000.0, quality, years)
        if quality == 0.0:
            flows = [f for f in flows if f["type"] in ("Call", "Fee")]
        last_flow = max(dt.date.fromisoformat(f["date"]) for f in flows)
        nav_date = (pd.Timestamp(last_flow) + pd.offsets.QuarterEnd(1)).date()
        paid_in = sum(f["amount"] for f in flows if f["type"] in ("Call", "Fee"))
        nav = round(paid_in * RNG.uniform(0.15, 0.9) * max(quality, 1.0), 2)
        index = gbm_index(first_day - dt.timedelta(days=30), nav_date + dt.timedelta(days=5), drift, volatility)

        dates = pd.Series(pd.to_datetime([f["date"] for f in flows]))
        amounts = pd.Series([signed(f) for f in flows])
        terminal = pd.Timestamp(nav_date)
        long_nickels = long_nickels_pme(dates, amounts, nav, terminal, index)
        cases.append({
            "name": name,
            "flows": flows,
            "nav": {"date": iso(nav_date), "value": nav},
            "index": index_points(index),
            "expected": {
                "kaplanSchoar": ks_pme(dates, amounts, nav, terminal, index),
                "directAlpha": nan_to_none(direct_alpha(dates, amounts, nav, terminal, index)),
                "longNickelsRatio": nan_to_none(long_nickels["ln_pme"]),
                "longNickelsShadowNav": None if long_nickels["shadow_negative"] else long_nickels["shadow_nav"],
                "longNickelsShadowNegative": bool(long_nickels["shadow_negative"]),
                **independent_pme_references(flows, nav, nav_date, index),
            },
        })
    return {
        "source": "privateassets==0.7.1 (KS-PME, Direct Alpha, Long-Nickels ratio); scipy reference (PME+, LN IRR, fund IRR). Day count ACT/365.25.",
        "dayCount": "Actual36525",
        "cases": cases,
    }


def independent_pme_references(flows: list[dict], nav: float, nav_date: dt.date, index: pd.Series) -> dict:
    """PME+ (Rouvinez 2003) and Long-Nickels IRR, written from the papers' definitions."""
    terminal_level = index.asof(pd.Timestamp(nav_date))
    dates = [dt.date.fromisoformat(f["date"]) for f in flows]
    contributions = [f["amount"] if f["type"] in ("Call", "Fee") else 0.0 for f in flows]
    distributions = [0.0 if f["type"] in ("Call", "Fee") else f["amount"] for f in flows]
    growth = [terminal_level / index.asof(pd.Timestamp(d)) for d in dates]
    fv_contributions = sum(c * g for c, g in zip(contributions, growth))
    fv_distributions = sum(d * g for d, g in zip(distributions, growth))
    actual = [d - c for c, d in zip(contributions, distributions)]

    days_per_year = 365.25
    compounded = [(d - c) * g for c, d, g in zip(contributions, distributions, growth)]
    result = {
        # privateassets brackets Direct Alpha on [-50%, 500%]; this reference has no such limit.
        "directAlphaReference": reference_xirr(dates + [nav_date], compounded + [nav], days_per_year),
        "fundIrr": reference_xirr(dates + [nav_date], actual + [nav], days_per_year),
        "longNickelsIrr": reference_xirr(dates + [nav_date], actual + [fv_contributions - fv_distributions], days_per_year),
        "longNickelsTerminalValue": fv_contributions - fv_distributions,
        "pmePlusScalingFactor": None,
        "pmePlusIrr": None,
    }
    if fv_distributions > 0:
        scaling = (fv_contributions - nav) / fv_distributions
        scaled = [scaling * d - c for c, d in zip(contributions, distributions)]
        result["pmePlusScalingFactor"] = scaling
        result["pmePlusIrr"] = reference_xirr(dates + [nav_date], scaled + [nav], days_per_year)
    return result


def mpme_dataset() -> dict:
    cases = []
    for case_number, (asset_drift, index_drift) in enumerate([(0.15, 0.07), (0.03, 0.09), (0.25, 0.10), (0.08, 0.08)]):
        count = int(RNG.integers(12, 25))
        start = random_business_day(dt.date(2012 + case_number, 1, 1), 100)
        dates = sorted({random_business_day(start, 365 * 9) for _ in range(count * 2)})[:count]
        dates[0] = start
        dates = sorted(set(dates))
        years = np.array([(d - start).days / 365.0 for d in dates])
        asset_prices = np.round(100 * np.exp(asset_drift * years + RNG.normal(0, 0.05, len(dates))), 4).tolist()
        pme_prices = np.round(100 * np.exp(index_drift * years + RNG.normal(0, 0.04, len(dates))), 4).tolist()

        cashflows = []
        nav_post = 0.0
        for k in range(len(dates) - 1):
            nav_pre = nav_post * asset_prices[k] / asset_prices[k - 1] if k > 0 else 0.0
            if k == 0 or (k < len(dates) // 2 and RNG.random() < 0.6):
                cf = -round(RNG.uniform(1e6, 5e6), 2)
                nav_post = nav_pre - cf
            else:
                cf = round(nav_pre * RNG.uniform(0.05, 0.45), 2)
                nav_post = nav_pre - cf
            cashflows.append(cf)

        pme_irr, asset_irr, frame = verbose_xpme(dates, cashflows, asset_prices, pme_prices)
        nav_after = frame["Asset", "NAVpost"].tolist()
        final_nav = frame["Asset", "NAVpre"].iloc[-1]
        flows = [
            {"date": iso(d), "amount": abs(cf), "type": "Call" if cf < 0 else "Distribution"}
            for d, cf in zip(dates[:-1], cashflows)
        ]
        navs = [{"date": iso(d), "value": v} for d, v in zip(dates[:-1], nav_after[:-1])]
        navs.append({"date": iso(dates[-1]), "value": final_nav})
        cases.append({
            "name": f"mpme-{case_number}",
            "flows": flows,
            "navs": navs,
            "index": [{"date": iso(d), "level": p} for d, p in zip(dates, pme_prices)],
            "asOf": iso(dates[-1]),
            "expected": {"modifiedPmeIrr": pme_irr, "fundIrr": asset_irr},
        })
    return {"source": "pypme==0.7.0 verbose_xpme (xirr==0.1.8, ACT/365)", "dayCount": "Actual365Fixed", "cases": cases}


def vintage_dataset() -> dict:
    as_of = dt.date(2025, 12, 31)
    funds = []
    for vintage in (2015, 2016, 2017, 2018):
        for number in range(int(RNG.integers(3, 6))):
            first_day = random_business_day(dt.date(vintage, 1, 1), 300)
            flows = synthetic_fund(first_day, float(RNG.choice([2e7, 5e7, 1e8])), float(RNG.uniform(0.6, 2.4)), 10)
            flows = [f for f in flows if dt.date.fromisoformat(f["date"]) <= as_of]
            paid_in = sum(f["amount"] for f in flows if f["type"] in ("Call", "Fee"))
            navs = []
            for year in range(vintage + 1, as_of.year + 1):
                navs.append({"date": f"{year}-12-31", "value": round(paid_in * RNG.uniform(0.2, 1.1), 2)})
            funds.append({"name": f"Fund {vintage}-{number + 1}", "vintageYear": vintage,
                          "commitment": 0.0, "flows": flows, "navs": navs})

    cash_flows = pd.DataFrame([
        {"fund": f["name"], "vintage_label": f["name"], "date": pd.Timestamp(c["date"]), "amount": signed(c),
         "kind": "contribution" if signed(c) < 0 else "distribution"}
        for f in funds for c in f["flows"]
    ])
    nav_frame = pd.DataFrame([
        {"fund": f["name"], "vintage_label": f["name"], "date": pd.Timestamp(n["date"]), "nav": n["value"]}
        for f in funds for n in f["navs"]
    ])
    stats = compute_vintage_stats(cash_flows, nav_frame).set_index("fund")

    for fund in funds:
        row = stats.loc[fund["name"]]
        fund["expected"] = {"dpi": row["DPI"], "rvpi": row["RVPI"], "tvpi": row["TVPI"], "irr": row["calc_net_IRR"]}

    vintages = []
    for vintage in sorted({f["vintageYear"] for f in funds}):
        members = [f for f in funds if f["vintageYear"] == vintage]
        pooled_dates, pooled_amounts = [], []
        for fund in members:
            pooled_dates += [dt.date.fromisoformat(c["date"]) for c in fund["flows"]] + [as_of]
            pooled_amounts += [signed(c) for c in fund["flows"]] + [fund["navs"][-1]["value"]]
        paid_in = sum(c["amount"] for f in members for c in f["flows"] if c["type"] in ("Call", "Fee"))
        distributed = sum(c["amount"] for f in members for c in f["flows"] if c["type"] not in ("Call", "Fee"))
        residual = sum(f["navs"][-1]["value"] for f in members)
        vintages.append({
            "vintageYear": vintage,
            "pooledTvpi": (distributed + residual) / paid_in,
            "pooledDpi": distributed / paid_in,
            "pooledIrr": reference_xirr(pooled_dates, pooled_amounts, 365.25),
            "tvpi": quartiles([f["expected"]["tvpi"] for f in members]),
            "irr": quartiles([f["expected"]["irr"] for f in members]),
        })
    return {
        "source": "privateassets==0.7.1 compute_vintage_stats (ACT/365.25); numpy.percentile linear; scipy pooled IRR",
        "dayCount": "Actual36525",
        "asOf": iso(as_of),
        "funds": funds,
        "vintages": vintages,
    }


def quartiles(values: list[float]) -> dict:
    return {
        "lowerQuartile": float(np.percentile(values, 25)),
        "median": float(np.percentile(values, 50)),
        "upperQuartile": float(np.percentile(values, 75)),
        "mean": float(np.mean(values)),
    }


def nan_to_none(value: float) -> float | None:
    return None if value is None or (isinstance(value, float) and math.isnan(value)) else float(value)


def write(name: str, payload: dict) -> None:
    path = OUTPUT_DIR / name
    path.write_text(json.dumps(payload, indent=1, default=float) + "\n", encoding="utf-8", newline="\n")
    print(f"wrote {path}")


def check(name: str, payload: dict, tolerance: float = 1e-9) -> bool:
    """Compare a regenerated dataset with the committed one, numbers to a relative tolerance."""
    committed = json.loads((OUTPUT_DIR / name).read_text(encoding="utf-8"))
    regenerated = json.loads(json.dumps(payload, default=float))
    mismatches = list(differences(committed, regenerated, tolerance, name))
    for mismatch in mismatches[:20]:
        print(mismatch)
    print(f"{name}: {'OK' if not mismatches else f'{len(mismatches)} difference(s)'}")
    return not mismatches


def differences(expected, actual, tolerance: float, path: str):
    if isinstance(expected, dict) and isinstance(actual, dict):
        for key in expected.keys() | actual.keys():
            yield from differences(expected.get(key), actual.get(key), tolerance, f"{path}.{key}")
    elif isinstance(expected, list) and isinstance(actual, list):
        if len(expected) != len(actual):
            yield f"{path}: {len(expected)} items committed, {len(actual)} regenerated"
            return
        for i, (e, a) in enumerate(zip(expected, actual)):
            yield from differences(e, a, tolerance, f"{path}[{i}]")
    elif isinstance(expected, (int, float)) and isinstance(actual, (int, float)) and not isinstance(expected, bool):
        if abs(expected - actual) > tolerance * max(1.0, abs(expected)):
            yield f"{path}: committed {expected!r}, regenerated {actual!r}"
    elif expected != actual:
        yield f"{path}: committed {expected!r}, regenerated {actual!r}"


if __name__ == "__main__":
    import sys

    datasets = {
        "xirr.json": xirr_dataset,
        "pme.json": pme_dataset,
        "mpme.json": mpme_dataset,
        "vintage.json": vintage_dataset,
    }
    if "--check" in sys.argv:
        results = [check(name, build()) for name, build in datasets.items()]
        sys.exit(0 if all(results) else 1)
    for name, build in datasets.items():
        write(name, build())
