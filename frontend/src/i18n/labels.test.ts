import { describe, expect, it } from 'vitest'
import { changeAppLanguage, locales } from './index'
import { presentationLabel } from './labels'
import english from './labels/en.json'
import { assertionStateLabel } from '../product/certificate-copy'
import { dailyReviewDuty } from '../product/daily-review-copy'

describe('operational label presentation', () => {
  it('authors all status label keys in each of the seventeen JSON catalogs', () => {
    const catalogs = import.meta.glob<{ default: Record<string, string> }>('./labels/*.json', { eager: true })
    for (const locale of locales) {
      const labels = catalogs[`./labels/${locale}.json`].default
      expect(Object.keys(labels).sort()).toEqual(Object.keys(english).sort())
      expect(Object.values(labels).every(value => typeof value === 'string' && value.trim().length > 0)).toBe(true)
    }
  })

  it('localizes duties and declaration text while the original server enum remains unchanged', async () => {
    await changeAppLanguage('ar', false)
    const state = 'Verified' as const, duty = 'ChiefEngineer' as const
    expect(assertionStateLabel(state, false)).toBe('مصرح بأنه متحقق منه')
    expect(dailyReviewDuty(duty, false)).toBe('كبير المهندسين')
    expect(state).toBe('Verified')
    expect(duty).toBe('ChiefEngineer')
    expect(presentationLabel('zh-Hans', 'verifiedDeclaration')).toBe('申报为已核实')
    expect(presentationLabel('zh-Hant', 'verifiedDeclaration')).toBe('申報為已核實')
  })

  it('keeps review and unverified declarations distinct from approved operational readiness', () => {
    expect(presentationLabel('en', 'declaredOk')).toBe('Declared OK (unverified)')
    expect(assertionStateLabel('Verified', true)).toBe('Declared verified')
    expect(presentationLabel('tr', 'reviewRequired')).toBe('İnceleme gerekli')
  })
})
