"""Versioned art direction and reproducible, ordered image instructions."""
from decimal import Decimal
from backend.provider import setting

from backend.prompts import ART_VERSION, compose


def prices():
    return {"qwenInputCny": float(Decimal(setting("QWEN_INPUT_IMAGE_PRICE_CNY", "0.02"))),
            "qwenOutputCny": float(Decimal(setting("QWEN_OUTPUT_IMAGE_PRICE_CNY", "0.18"))),
            "aholoCredits": float(Decimal(setting("AHOLO_MODEL_PRICE_CREDITS", "1"))),
            "isEstimate": True,
            "qwenSource": "https://www.qianwenai.com/models/qwen-image-3.0",
            "aholoSource": "https://www.aholo3d.cn/pricing"}


def image_cost(input_count, output_count=1, basis="estimate"):
    p = prices()
    amount = Decimal(str(p["qwenInputCny"])) * input_count + Decimal(str(p["qwenOutputCny"])) * output_count
    return {"currency": "CNY", "amount": float(amount), "inputImageCount": input_count,
            "outputImageCount": output_count, "inputUnitPriceCny": p["qwenInputCny"],
            "outputUnitPriceCny": p["qwenOutputCny"], "basis": basis, "isEstimate": True}
