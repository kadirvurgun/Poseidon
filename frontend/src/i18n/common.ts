import ar from './common/ar.json'
import cs from './common/cs.json'
import hu from './common/hu.json'
import fr from './common/fr.json'
import en from './common/en.json'
import fi from './common/fi.json'
import enGB from './common/en-GB.json'
import zhHans from './common/zh-Hans.json'
import hi from './common/hi.json'
import sk from './common/sk.json'
import tr from './common/tr.json'
import ru from './common/ru.json'
import ptBR from './common/pt-BR.json'
import it from './common/it.json'
import es from './common/es.json'
import de from './common/de.json'
import zhHant from './common/zh-Hant.json'
import type { Locale } from './locales'

export type CommonMessageKey = keyof typeof en
// These are authored UI translations, not regulatory or expert approvals.
export const commonMessages: Record<Locale, Record<CommonMessageKey, string>> = {
  ar, cs, hu, fr, en, fi, 'en-GB': enGB, 'zh-Hans': zhHans, hi, sk, tr, ru, 'pt-BR': ptBR, it, es, de, 'zh-Hant': zhHant,
}
