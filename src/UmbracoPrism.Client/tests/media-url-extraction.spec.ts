import { test, expect } from '@playwright/test';

// Pure logic test — no browser needed
// Tests that the media URL extraction logic (used in _pickMediaForVariable)
// correctly parses the /media/urls API response format.

function extractMediaUrl(data: unknown): string {
  const items: Array<{ id: string; urlInfos: Array<{ culture: string | null; url: string | null }> }> =
    Array.isArray(data) ? data : [data as { id: string; urlInfos: Array<{ culture: string | null; url: string | null }> }];
  return items[0]?.urlInfos?.[0]?.url ?? '';
}

test.describe('Media URL extraction', () => {
  test('extracts url from /media/urls API response', () => {
    const response = [{ id: 'abc-123', urlInfos: [{ culture: null, url: '/media/foo.jpg' }] }];
    expect(extractMediaUrl(response)).toBe('/media/foo.jpg');
  });

  test('returns empty string when urlInfos is empty', () => {
    const response = [{ id: 'abc-123', urlInfos: [] }];
    expect(extractMediaUrl(response)).toBe('');
  });

  test('returns empty string when url is null', () => {
    const response = [{ id: 'abc-123', urlInfos: [{ culture: null, url: null }] }];
    expect(extractMediaUrl(response)).toBe('');
  });

  test('returns empty string when response is empty array', () => {
    expect(extractMediaUrl([])).toBe('');
  });
});
