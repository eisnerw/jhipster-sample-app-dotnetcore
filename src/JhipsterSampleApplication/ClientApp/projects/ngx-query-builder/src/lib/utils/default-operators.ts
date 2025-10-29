export type DefaultOperatorMap = Record<string, string[]>;

const BASE_DEFAULT_OPERATOR_MAP: DefaultOperatorMap = {
  string: ['=', '!=', 'contains', '!contains', 'like', '!like', 'in', '!in', 'exists'],
  number: ['=', '!=', '>', '>=', '<', '<=', 'in', '!in', 'exists'],
  time: ['=', '!=', '>', '>=', '<', '<=', 'in', '!in', 'exists'],
  date: ['=', '!=', '>', '>=', '<', '<=', 'in', '!in', 'exists'],
  category: ['=', '!=', 'in', '!in', 'exists'],
  boolean: ['=', '!=', 'exists'],
};

const FALLBACK_OPERATORS = ['=', '!=', 'exists'];

/**
 * Returns a shallow clone of the default operator map so callers can extend it
 * without mutating the shared baseline definition.
 */
export function createDefaultOperatorMap(): DefaultOperatorMap {
  const clone: DefaultOperatorMap = {};
  for (const [fieldType, operators] of Object.entries(BASE_DEFAULT_OPERATOR_MAP)) {
    clone[fieldType] = [...operators];
  }
  return clone;
}

/**
 * Returns the default operators for a given field type, falling back to a minimal set.
 */
export function getDefaultOperatorsForFieldType(fieldType: string): string[] {
  const operators = BASE_DEFAULT_OPERATOR_MAP[fieldType];
  return operators ? [...operators] : [...FALLBACK_OPERATORS];
}
