// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Vorssaint

import Foundation

struct SwitcherCloseState: Equatable {
    let remainingItemIDs: [String]
    let selectedIndex: Int
    let didRemove: Bool
    let shouldEndSession: Bool
}

enum SwitcherSupport {
    static func closeState(afterRemoving closedItemID: String,
                           itemIDs: [String],
                           selectedIndex: Int) -> SwitcherCloseState {
        guard let removedIndex = itemIDs.firstIndex(of: closedItemID) else {
            return SwitcherCloseState(
                remainingItemIDs: itemIDs,
                selectedIndex: clampedSelection(selectedIndex, count: itemIDs.count),
                didRemove: false,
                shouldEndSession: itemIDs.isEmpty
            )
        }

        let currentIndex = clampedSelection(selectedIndex, count: itemIDs.count)
        let remaining = itemIDs.filter { $0 != closedItemID }
        guard !remaining.isEmpty else {
            return SwitcherCloseState(remainingItemIDs: [],
                                      selectedIndex: 0,
                                      didRemove: true,
                                      shouldEndSession: true)
        }

        let nextIndex: Int
        if removedIndex < currentIndex {
            nextIndex = currentIndex - 1
        } else if removedIndex == currentIndex {
            nextIndex = min(currentIndex, remaining.count - 1)
        } else {
            nextIndex = currentIndex
        }

        return SwitcherCloseState(remainingItemIDs: remaining,
                                  selectedIndex: clampedSelection(nextIndex, count: remaining.count),
                                  didRemove: true,
                                  shouldEndSession: false)
    }

    private static func clampedSelection(_ index: Int, count: Int) -> Int {
        guard count > 0 else { return 0 }
        return min(max(0, index), count - 1)
    }
}
