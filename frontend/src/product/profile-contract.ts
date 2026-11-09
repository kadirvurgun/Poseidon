import { sourceText } from "../i18n/messages";
import type { components } from '../generated/api';
// Additive authored contract; regenerate the actual schema at the combined gate.
export type ProfileContent = components['schemas']['RegulatoryDraftContent'] & {
    mainPropulsionKw: NumberValue;
    contractForConstructionDate: TextValue;
    constructionRecordReference: TextValue;
    constructionRecord: {
        id: string;
        revision: number;
        checksum: string;
    } | null;
};
export type Profile = Omit<components['schemas']['RegulatoryDraftDto'], 'content'> & {
    content: ProfileContent;
};
export type ProfileCommand = Omit<components['schemas']['SaveRegulatoryDraft'], 'content'> & {
    content: ProfileContent;
};
type HistoryRow = components['schemas']['PageDtoOfRegulatoryRevisionDto']['items'][number];
export type ProfileHistory = Omit<components['schemas']['PageDtoOfRegulatoryRevisionDto'], 'items'> & {
    items: (Omit<HistoryRow, 'content'> & {
        content: ProfileContent;
    })[];
};
export type TextValue = components['schemas']['TextInput'];
export type NumberValue = components['schemas']['NumberInput'];
// Presentation metadata only. Server owns field validation, scope and draft lifecycle.
export const profileFields = [
    ['imo', 'text', sourceText("tr", "pairs.m_76a9d7c6a28d152e"), sourceText("en", "pairs.m_76a9d7c6a28d152e")], ['flag', 'text', sourceText("tr", "pairs.m_89f14ddcc216bfff"), sourceText("en", "pairs.m_89f14ddcc216bfff")],
    ['shipType', 'text', sourceText("tr", "pairs.m_74d2ca24a5449a18"), sourceText("en", "pairs.m_74d2ca24a5449a18")], ['classSociety', 'text', sourceText("tr", "pairs.m_d6f66d83374b622b"), sourceText("en", "pairs.m_d6f66d83374b622b")],
    ['classNumber', 'text', sourceText("tr", "pairs.m_035db1aefed446d5"), sourceText("en", "pairs.m_035db1aefed446d5")], ['gt', 'number', sourceText("tr", "pairs.m_289093ee560f5345"), sourceText("en", "pairs.m_289093ee560f5345")],
    ['nt', 'number', sourceText("tr", "pairs.m_798fbdec069f981b"), sourceText("en", "pairs.m_798fbdec069f981b")], ['dwt', 'number', sourceText("tr", "pairs.m_28970cc0fc721355"), sourceText("en", "pairs.m_28970cc0fc721355")], ['loa', 'number', sourceText("tr", "pairs.m_81cec80ea1ec251e"), sourceText("en", "pairs.m_81cec80ea1ec251e")],
    ['breadth', 'number', sourceText("tr", "pairs.m_442dd536fb100edf"), sourceText("en", "pairs.m_442dd536fb100edf")], ['draft', 'number', sourceText("tr", "pairs.m_0e2d3e4f17451531"), sourceText("en", "pairs.m_0e2d3e4f17451531")],
    ['buildDate', 'text', sourceText("tr", "pairs.m_f47538885f1648e1"), sourceText("en", "pairs.m_f47538885f1648e1")],
    ['mainPropulsionKw', 'number', sourceText("tr", "pairs.m_3435afddeb80d92e"), sourceText("en", "pairs.m_3435afddeb80d92e")],
    ['contractForConstructionDate', 'text', sourceText("tr", "pairs.m_dec829ba08e6bc58"), sourceText("en", "pairs.m_dec829ba08e6bc58")],
    ['constructionRecordReference', 'text', sourceText("tr", "pairs.m_cc3603d37de08e70"), sourceText("en", "pairs.m_cc3603d37de08e70")],
    ['tradingArea', 'text', sourceText("tr", "pairs.m_fef35f1e36a8aabf"), sourceText("en", "pairs.m_fef35f1e36a8aabf")], ['cargoContext', 'text', sourceText("tr", "pairs.m_1c28884f28a26286"), sourceText("en", "pairs.m_1c28884f28a26286")],
    ['declaration', 'text', sourceText("tr", "pairs.m_70fc268ddd724fe0"), sourceText("en", "pairs.m_70fc268ddd724fe0")],
    ['sourceReference', 'text', sourceText("tr", "pairs.m_2e37798ea762793a"), sourceText("en", "pairs.m_2e37798ea762793a")],
    ['sourceObservedAt', 'text', sourceText("tr", "pairs.m_b48181f8a5dda15d"), sourceText("en", "pairs.m_b48181f8a5dda15d")],
] as const;
