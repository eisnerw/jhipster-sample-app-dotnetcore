import { jsonToBql } from './bql-converter';

describe('jsonToBql', () => {
  it('converts regex without quotes', () => {
    const q = { document: { CONTAINS: '/ani/' } };
    expect(jsonToBql(q)).toBe('document CONTAINS /ani/');
  });

  it('converts regex with flags', () => {
    const q = { document: { CONTAINS: '/dani/i' } };
    expect(jsonToBql(q)).toBe('document CONTAINS /dani/i');
  });

  it('converts RegExp instance', () => {
    const q = { document: { CONTAINS: /ani/ } };
    expect(jsonToBql(q)).toBe('document CONTAINS /ani/');
  });

  it('wraps normal strings in quotes', () => {
    const q = { document: { CONTAINS: 'ani' } };
    expect(jsonToBql(q)).toBe('document CONTAINS "ani"');
  });
});
