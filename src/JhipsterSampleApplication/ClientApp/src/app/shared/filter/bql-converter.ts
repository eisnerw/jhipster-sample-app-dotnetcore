export interface BqlCondition {
  CONTAINS?: unknown;
  [key: string]: unknown;
}

export type BqlJson = Record<string, unknown>;

const regexLike = /^\/.*\/[gimsuy]*$/;

function isRegexExpression(value: unknown): value is string {
  return typeof value === 'string' && regexLike.test(value);
}

export function jsonToBql(json: BqlJson): string {
  const [field, condition] = Object.entries(json)[0] ?? [];
  if (!field || typeof condition !== 'object' || condition === null) {
    return '';
  }

  const cond = condition as BqlCondition;

  if ('CONTAINS' in cond) {
    const raw = cond.CONTAINS;
    let expression: string;
    if (raw instanceof RegExp) {
      expression = raw.toString();
    } else if (isRegexExpression(raw)) {
      expression = raw;
    } else {
      expression = `"${String(raw)}"`;
    }
    return `${field} CONTAINS ${expression}`;
  }

  return '';
}

export { isRegexExpression };
